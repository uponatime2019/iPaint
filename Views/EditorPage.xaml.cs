using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using System.Runtime.InteropServices.WindowsRuntime;
using iPaint.Controls;
using iPaint.Models;
using iPaint.Services;
using WinUI = Microsoft.UI.Xaml;

namespace iPaint.Views
{
    public enum EditorTool
    {
        Transform, MagicWand, Lasso, FxFilter, Brush, Eraser, Smudge, Blur, SpecialPen, Bucket
    }

    public sealed partial class EditorPage : Page
    {
        private ArtworkDocument _doc = null!;
        private WriteableBitmap _bitmap = null!;
        private byte[] _composite = null!;
        private ArtworkDocument? _pendingDoc;
        private bool _initialized;
        private int _activeLayerIndex;
        private Color _color = Colors.Black;
        private Color _prevColor = Colors.White;

        private BrushSettings _brush = new();
        private EditorTool _tool = EditorTool.Brush;
        private readonly List<EditorTool> _railOrder = new()
        {
            EditorTool.Transform, EditorTool.MagicWand, EditorTool.Lasso, EditorTool.FxFilter,
            EditorTool.Brush, EditorTool.Eraser, EditorTool.Smudge, EditorTool.Blur,
            EditorTool.SpecialPen, EditorTool.Bucket
        };

        // stroke state
        private bool _drawing;
        private double _lastX, _lastY;
        private double _smoothX, _smoothY;
        private double _strokeStartX, _strokeStartY;
        private double _strokeLength;
        private readonly List<Point> _lassoPoints = new();
        private Rect _strokeBounds;
        private DateTime _strokeStart = DateTime.Now;

        // undo
        private readonly Stack<DocumentSnapshot> _undo = new();
        private readonly Stack<DocumentSnapshot> _redo = new();
        private long _undoBytes;

        // selection mask
        private byte[]? _selectionMask;
        private Rect _selectionRect;

        // rulers
        public string RulerMode { get; private set; } = "Off";
        public string SymmetryMode { get; private set; } = "Off";

        // stabilizer
        private double _stabilizer => MainWindow.Current.Settings.StabilizerValue;
        private bool PanMode;

        private double _zoom = 1.0;
        private string _currentDocId;
        private bool _dirty;

        public EditorPage()
        {
            // Frame.Navigate(typeof(EditorPage), doc) activates this ctor; the document
            // arrives via OnNavigatedTo — do not build document state here.
            InitializeComponent();
        }

        public EditorPage(ArtworkDocument doc) : this()
        {
            // Direct instantiation support: OnNavigatedTo never fires on this path.
            _pendingDoc = doc;
            Loaded += (s, e) => EnsureInitialized();
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is ArtworkDocument doc) _pendingDoc = doc;
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _doc = _pendingDoc ?? new ArtworkDocument { Width = 2048, Height = 2048 };
            _composite = new byte[_doc.Width * _doc.Height * 4];
            Init();
        }

        private void Init()
        {
            if (_doc.Layers.Count == 0)
                _doc.Layers.Add(new PaintLayer(_doc.Width, _doc.Height, false));
            _activeLayerIndex = _doc.Layers.Count - 1;

            BuildToolRail();
            BuildToolPanelContent();
            BuildLayerPanel();
            BuildColorPanel();
            // Flyouts are built on demand from their button handlers — opening a popup
            // during the ctor fails ("unparented popup" has no XamlRoot yet).

            _bitmap = new WriteableBitmap(_doc.Width, _doc.Height);
            CanvasImage.Source = _bitmap;
            CanvasHolder.Width = _doc.Width;
            CanvasHolder.Height = _doc.Height;
            CanvasGrid.Width = _doc.Width;
            CanvasGrid.Height = _doc.Height;
            TransformFrame.Width = _doc.Width;
            TransformFrame.Height = _doc.Height;
            ReferenceImage.Source = _bitmap;

            MainWindow.Current.Title = $"{_doc.Title} - iPaint - Draw Anything";
            CanvasScroller.MinZoomFactor = 0.1f;
            CanvasScroller.MaxZoomFactor = 32f;
            // Slider ranges set here (not in XAML) so no ValueChanged fires mid-parse (crash-fix Rule 1).
            // Capture the brush size first: setting Minimum coerces Value and fires ValueChanged.
            double brushSize = _brush.Size;
            SizeSlider.Minimum = 1;
            SizeSlider.Maximum = 100;
            SizeSlider.Value = brushSize;
            ZoomSlider.Minimum = 5;
            ZoomSlider.Maximum = 3200;
            ZoomSlider.Value = 100;
            _brush.Size = brushSize;
            LayerCountText.Text = Math.Max(1, _doc.Layers.Count(l => !l.IsBackground)).ToString();
            FullComposite();
            ApplyGridOverlay();
            UpdateSizePreview();

            Loaded += OnEditorLoaded;
        }

        private void OnEditorLoaded(object sender, RoutedEventArgs e)
        {
            ZoomToFit();
        }

        /// <summary>Zooms the canvas to fit the viewport, like the original's initial view.</summary>
        private void ZoomToFit()
        {
            double vw = CanvasScroller.ViewportWidth;
            double vh = CanvasScroller.ViewportHeight;
            if (vw <= 0 || vh <= 0) return;
            double fit = Math.Min(vw / _doc.Width, vh / _doc.Height);
            fit = Math.Clamp(fit, CanvasScroller.MinZoomFactor, CanvasScroller.MaxZoomFactor);
            ZoomSlider.Value = fit * 100;
        }

        private PaintLayer ActiveLayer => _doc.Layers[Math.Clamp(_activeLayerIndex, 0, _doc.Layers.Count - 1)];

        // ================= rendering =================

        private void FullComposite()
        {
            if (_bitmap == null || _composite == null) return; // panel callbacks can run before Init finishes
            BrushEngine.Composite(_doc.Layers, _doc.Width, _doc.Height, _doc.BackgroundType, _composite);
            BlitRegion(0, 0, _doc.Width, _doc.Height);
        }

        private void BlitRegion(int rx, int ry, int rw, int rh)
        {
            rx = Math.Max(0, rx);
            ry = Math.Max(0, ry);
            rw = Math.Min(_doc.Width - rx, rw);
            rh = Math.Min(_doc.Height - ry, rh);
            if (rw <= 0 || rh <= 0) return;

            using var stream = _bitmap.PixelBuffer.AsStream();
            int stride = _doc.Width * 4;
            if (rw == _doc.Width)
            {
                stream.Position = (long)ry * stride;
                stream.Write(_composite, ry * stride, rh * stride);
            }
            else
            {
                for (int y = 0; y < rh; y++)
                {
                    long pos = (long)(ry + y) * stride + rx * 4;
                    stream.Position = pos;
                    stream.Write(_composite, (ry + y) * stride + rx * 4, rw * 4);
                }
            }
            _bitmap.Invalidate();
            ReferenceImage.InvalidateArrange();
        }

        // ================= pointer / drawing =================

        private void OnCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(CanvasImage);
            if (pt.Properties.IsMiddleButtonPressed || PanMode)
                return; // ScrollViewer handles pan

            var p = ToCanvasPoint(e);
            if (p == null) return;
            _drawing = true;
            CanvasImage.CapturePointer(e.Pointer);
            SetToolFloatGhost(true);

            PushUndoSnapshot();

            _lastX = _smoothX = _strokeStartX = p.Value.X;
            _lastY = _smoothY = _strokeStartY = p.Value.Y;
            _strokeLength = 0;
            _strokeStart = DateTime.Now;
            _strokeBounds = new Rect(p.Value.X, p.Value.Y, 0, 0);
            _lassoPoints.Clear();
            _lassoPoints.Add(p.Value);

            var tool = _tool;
            if (tool == EditorTool.MagicWand)
            {
                MagicWandPick(p.Value);
                _drawing = false;
                CanvasImage.ReleasePointerCapture(e.Pointer);
                SetToolFloatGhost(false);
                return;
            }
            if (tool == EditorTool.Lasso)
            {
                return; // collect points until release
            }
            if (tool == EditorTool.Bucket)
            {
                BrushEngine.FloodFill(ActiveLayer, (int)p.Value.X, (int)p.Value.Y, _color, 0.06);
                FullComposite();
                _drawing = false;
                CanvasImage.ReleasePointerCapture(e.Pointer);
                SetToolFloatGhost(false);
                return;
            }
            if (tool == EditorTool.Transform)
            {
                return;
            }

            // draw a first dab
            StrokeSegment(p.Value.X, p.Value.Y, p.Value.X + 0.01, p.Value.Y + 0.01, 0);
        }

        private void OnCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_drawing) return;
            var p = ToCanvasPoint(e);
            if (p == null) return;

            double rawX = p.Value.X, rawY = p.Value.Y;

            if (_tool == EditorTool.Lasso)
            {
                _lassoPoints.Add(p.Value);
                return;
            }

            double sx = rawX, sy = rawY;
            int stab = (int)_stabilizer;
            if (stab > 0)
            {
                double k = Math.Clamp(1.0 - stab / 18.0, 0.05, 1.0);
                sx = _smoothX + (rawX - _smoothX) * k;
                sy = _smoothY + (rawY - _smoothY) * k;
                _smoothX = sx;
                _smoothY = sy;
            }

            double dx = sx - _lastX, dy = sy - _lastY;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 0.4) return;

            bool rulerApplied = ApplyRuler(ref sx, ref sy);

            StrokeSegment(_lastX, _lastY, sx, sy, _strokeLength / Math.Max(60, _strokeLength + dist));
            _strokeLength += dist;

            if (!rulerApplied)
            {
                _lastX = sx;
                _lastY = sy;
            }
        }

        private void OnCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_drawing) return;
            _drawing = false;
            SetToolFloatGhost(false);

            if (_tool == EditorTool.Lasso)
            {
                BuildLassoMask();
                return;
            }

            // "After" stabilizer: smooth the final point of the stroke by re-drawing a softened pass
            if (_stabilizer > 0 && MainWindow.Current.Settings.StabilizerMethod == "After" && _strokeBounds.Width > 0)
            {
                // light smoothing pass already applied in real time; final pass is a no-op for now
            }

            _dirty = true;
            FullComposite();
        }

        private bool ApplyRuler(ref double x, ref double y)
        {
            switch (RulerMode)
            {
                case "Linear":
                {
                    // constrain to the dominant axis of the stroke from its start point
                    double dx = x - _strokeStartX, dy = y - _strokeStartY;
                    if (Math.Abs(dx) > Math.Abs(dy)) y = _strokeStartY;
                    else x = _strokeStartX;
                    return true;
                }
                default:
                    return false;
            }
        }

        private void StrokeSegment(double x0, double y0, double x1, double y1, double progress)
        {
            var layer = ActiveLayer;
            var brush = _brush;

            // symmetry stamping
            int stamps = SymmetryMode switch
            {
                "Vertical" => 2,
                "Horizontal" => 2,
                "Quad" => 4,
                "Radial 8" => 8,
                _ => 1
            };

            for (int i = 0; i < stamps; i++)
            {
                double ax0 = x0, ay0 = y0, ax1 = x1, ay1 = y1;
                if (stamps > 1)
                {
                    double cx = _doc.Width / 2.0, cy = _doc.Height / 2.0;
                    switch (SymmetryMode)
                    {
                        case "Vertical":
                            if (i == 1) { ax0 = _doc.Width - x0; ax1 = _doc.Width - x1; }
                            break;
                        case "Horizontal":
                            if (i == 1) { ay0 = _doc.Height - y0; ay1 = _doc.Height - y1; }
                            break;
                        case "Quad":
                            if (i == 1) { ax0 = _doc.Width - x0; ax1 = _doc.Width - x1; }
                            else if (i == 2) { ay0 = _doc.Height - y0; ay1 = _doc.Height - y1; }
                            else { ax0 = _doc.Width - x0; ax1 = _doc.Width - x1; ay0 = _doc.Height - y0; ay1 = _doc.Height - y1; }
                            break;
                        case "Radial 8":
                            (ax0, ay0) = RotateAround(cx, cy, x0, y0, i * 45.0);
                            (ax1, ay1) = RotateAround(cx, cy, x1, y1, i * 45.0);
                            break;
                    }
                }

                BrushEngine.DrawSegment(layer, ax0, ay0, ax1, ay1, _color, brush,
                    pressureStart: 1.0, pressureEnd: 1.0, progress);

                _strokeBounds.Union(new Rect(
                    Math.Min(ax0, ax1) - brush.Size, Math.Min(ay0, ay1) - brush.Size,
                    Math.Abs(ax1 - ax0) + brush.Size * 2 + 2, Math.Abs(ay1 - ay0) + brush.Size * 2 + 2));
            }

            // composite dirty region
            var r = _strokeBounds;
            int rx = (int)Math.Max(0, Math.Floor(r.X));
            int ry = (int)Math.Max(0, Math.Floor(r.Y));
            int rw = (int)Math.Min(_doc.Width - rx, Math.Ceiling(r.Width) + 1);
            int rh = (int)Math.Min(_doc.Height - ry, Math.Ceiling(r.Height) + 1);
            BrushEngine.CompositeRegion(_doc.Layers, _doc.Width, _doc.Height, _doc.BackgroundType, _composite, rx, ry, rw, rh);
            BlitRegion(rx, ry, rw, rh);
        }

        private static (double, double) RotateAround(double cx, double cy, double x, double y, double deg)
        {
            double rad = deg * Math.PI / 180;
            double cos = Math.Cos(rad), sin = Math.Sin(rad);
            double dx = x - cx, dy = y - cy;
            return (cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
        }

        private Point? ToCanvasPoint(PointerRoutedEventArgs e)
        {
            // GetCurrentPoint(relativeTo) already yields coordinates in the canvas pixel space
            // (render transform included) — no extra zoom division needed.
            var p = e.GetCurrentPoint(CanvasImage).Position;
            if (p.X < 0 || p.Y < 0 || p.X >= _doc.Width || p.Y >= _doc.Height) return null;
            return p;
        }

        private void OnCanvasWheel(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(CanvasImage);
            if (!point.Properties.IsHorizontalMouseWheel && e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control))
            {
                double delta = point.Properties.MouseWheelDelta;
                double newZoom = Math.Clamp(_zoom * (delta > 0 ? 1.15 : 1 / 1.15), 0.05, 32);
                ZoomSlider.Value = newZoom * 100;
                e.Handled = true;
            }
        }

        // ================= selection =================

        private void MagicWandPick(Point p)
        {
            int w = _doc.Width, h = _doc.Height;
            var mask = new byte[w * h];
            int sx = (int)p.X, sy = (int)p.Y;
            if (sx < 0 || sy < 0 || sx >= w || sy >= h) return;
            var layer = ActiveLayer;
            var px = layer.BgraPixels;
            int start = (sy * w + sx) * 4;
            byte tB = px[start], tG = px[start + 1], tR = px[start + 2], tA = px[start + 3];

            var stack = new Stack<int>();
            stack.Push(sy * w + sx);
            var visited = new bool[w * h];
            double minX = w, minY = h, maxX = 0, maxY = 0;

            while (stack.Count > 0)
            {
                int pos = stack.Pop();
                if (visited[pos]) continue;
                visited[pos] = true;
                int x = pos % w, y = pos / w;
                int o = pos * 4;
                if (Math.Abs(px[o] - tB) > 15 || Math.Abs(px[o + 1] - tG) > 15 ||
                    Math.Abs(px[o + 2] - tR) > 15 || Math.Abs(px[o + 3] - tA) > 15)
                    continue;

                mask[pos] = 255;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);

                if (x > 0) stack.Push(pos - 1);
                if (x < w - 1) stack.Push(pos + 1);
                if (y > 0) stack.Push(pos - w);
                if (y < h - 1) stack.Push(pos + w);
            }

            if (maxX >= minX && maxY >= minY)
            {
                _selectionMask = mask;
                _selectionRect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                SelectionOutline.Margin = new Thickness(_selectionRect.X, _selectionRect.Y, 0, 0);
                SelectionOutline.Width = _selectionRect.Width;
                SelectionOutline.Height = _selectionRect.Height;
                SelectionOutline.Visibility = Visibility.Visible;
            }
        }

        private void BuildLassoMask()
        {
            if (_lassoPoints.Count < 3)
            {
                return;
            }
            int w = _doc.Width, h = _doc.Height;
            var mask = new byte[w * h];
            var pts = _lassoPoints;

            for (int y = 0; y < h; y++)
            {
                double[] nodes = new double[pts.Count];
                int crossings = 0;
                for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                {
                    if ((pts[i].Y <= y && pts[j].Y > y) || (pts[j].Y <= y && pts[i].Y > y))
                        nodes[crossings++] = pts[i].X + (y - pts[i].Y) / (pts[j].Y - pts[i].Y) * (pts[j].X - pts[i].X);
                }
                Array.Sort(nodes, 0, crossings);
                for (int i = 0; i + 1 < crossings; i += 2)
                {
                    int x0 = Math.Max(0, (int)Math.Ceiling(nodes[i]));
                    int x1 = Math.Min(w - 1, (int)nodes[i + 1]);
                    for (int x = x0; x <= x1; x++) mask[y * w + x] = 255;
                }
            }

            _selectionMask = mask;
            double minX = pts.Min(p2 => p2.X), minY = pts.Min(p2 => p2.Y);
            double maxX = pts.Max(p2 => p2.X), maxY = pts.Max(p2 => p2.Y);
            _selectionRect = new Rect(minX, minY, maxX - minX, maxY - minY);
            SelectionOutline.Margin = new Thickness(minX, minY, 0, 0);
            SelectionOutline.Width = _selectionRect.Width;
            SelectionOutline.Height = _selectionRect.Height;
            SelectionOutline.Visibility = Visibility.Visible;
        }

        private void ClearSelection()
        {
            _selectionMask = null;
            SelectionOutline.Visibility = Visibility.Collapsed;
        }

        private void InvertSelection()
        {
            if (_selectionMask == null) return;
            for (int i = 0; i < _selectionMask.Length; i++)
                _selectionMask[i] = _selectionMask[i] == 0 ? (byte)255 : (byte)0;
            _selectionRect = new Rect(0, 0, _doc.Width, _doc.Height);
            SelectionOutline.Margin = new Thickness(0);
            SelectionOutline.Width = _doc.Width;
            SelectionOutline.Height = _doc.Height;
            SelectionOutline.Visibility = Visibility.Visible;
        }

        private void SelectByOpacity()
        {
            int w = _doc.Width, h = _doc.Height;
            var px = ActiveLayer.BgraPixels;
            var mask = new byte[w * h];
            for (int i = 0; i < w * h; i++)
                mask[i] = px[i * 4 + 3] > 8 ? (byte)255 : (byte)0;
            _selectionMask = mask;
            _selectionRect = new Rect(0, 0, w, h);
            SelectionOutline.Margin = new Thickness(0);
            SelectionOutline.Width = w;
            SelectionOutline.Height = h;
            SelectionOutline.Visibility = Visibility.Visible;
        }

        // ================= undo / redo =================

        private void PushUndoSnapshot()
        {
            var snap = new DocumentSnapshot
            {
                Layers = _doc.Layers.Select(l => l.Clone()).ToList(),
                Title = _doc.Title,
                BackgroundType = _doc.BackgroundType
            };
            long bytes = _doc.Layers.Sum(l => (long)l.BgraPixels.Length);
            if (_undoBytes + bytes > 256_000_000 && _undo.Count > 0)
            {
                var dropped = _undo.Pop();
                _undoBytes -= dropped.Layers.Sum(l => (long)l.BgraPixels.Length);
            }
            _undo.Push(snap);
            _undoBytes += bytes;
            _redo.Clear();
            RedoButton.Foreground = Col.Brush("#8A8A8A");
            UndoButton.Foreground = Col.Brush(Col.Text);
        }

        private void RestoreSnapshot(DocumentSnapshot snap)
        {
            _doc.Layers = snap.Layers.Select(l => l.Clone()).ToList();
            _doc.BackgroundType = snap.BackgroundType;
            if (_activeLayerIndex >= _doc.Layers.Count) _activeLayerIndex = _doc.Layers.Count - 1;
            LayerCountText.Text = Math.Max(1, _doc.Layers.Count(l => !l.IsBackground)).ToString();
            BuildLayerPanel();
            FullComposite();
        }

        private void OnUndoClick(object sender, RoutedEventArgs e)
        {
            if (_undo.Count == 0) return;
            var current = new DocumentSnapshot
            {
                Layers = _doc.Layers.Select(l => l.Clone()).ToList(),
                Title = _doc.Title,
                BackgroundType = _doc.BackgroundType
            };
            _redo.Push(current);
            RestoreSnapshot(_undo.Pop());
            RedoButton.Foreground = Col.Brush(Col.Text);
            if (_undo.Count == 0) UndoButton.Foreground = Col.Brush("#8A8A8A");
        }

        private void OnRedoClick(object sender, RoutedEventArgs e)
        {
            if (_redo.Count == 0) return;
            var current = new DocumentSnapshot
            {
                Layers = _doc.Layers.Select(l => l.Clone()).ToList(),
                Title = _doc.Title,
                BackgroundType = _doc.BackgroundType
            };
            _undo.Push(current);
            RestoreSnapshot(_redo.Pop());
            UndoButton.Foreground = Col.Brush(Col.Text);
            if (_redo.Count == 0) RedoButton.Foreground = Col.Brush("#8A8A8A");
        }
    }
}
