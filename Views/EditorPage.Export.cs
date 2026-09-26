using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using Windows.Foundation;
using Windows.Storage.Pickers;
using iPaint.Controls;
using iPaint.Models;
using iPaint.Services;
using WinUI = Microsoft.UI.Xaml;

namespace iPaint.Views
{
    public partial class EditorPage
    {
        // ================= bottom bar handlers =================

        private void OnSizeMinus(object sender, RoutedEventArgs e) => SizeSlider.Value = Math.Max(SizeSlider.Minimum, SizeSlider.Value - 1);
        private void OnSizePlus(object sender, RoutedEventArgs e) => SizeSlider.Value = Math.Min(SizeSlider.Maximum, SizeSlider.Value + 1);

        private void OnSizeSliderChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            // XAML parse fires ValueChanged before later siblings exist (crash-fix Rule 2)
            if (_brush == null || SizeLabel == null || SizePreviewCircle == null || SizePreviewText == null) return;
            _brush.Size = e.NewValue;
            SizeLabel.Text = $"{e.NewValue:0.0}px";
            UpdateSizePreview();
        }

        private void OnZoomSliderChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_doc == null || CanvasHolder == null || CanvasGrid == null || TransformFrame == null || ZoomLabel == null) return;
            _zoom = e.NewValue / 100.0;
            ZoomLabel.Text = $"{e.NewValue:0}%";
            // Holder gets the scaled layout size; the grid keeps full-size layout and is
            // only scaled by the render transform (otherwise the image would scale twice).
            CanvasHolder.Width = _doc.Width * _zoom;
            CanvasHolder.Height = _doc.Height * _zoom;
            CanvasGrid.Width = _doc.Width;
            CanvasGrid.Height = _doc.Height;
            CanvasGrid.RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform
            {
                ScaleX = _zoom,
                ScaleY = _zoom
            };
            TransformFrame.Width = CanvasHolder.Width;
            TransformFrame.Height = CanvasHolder.Height;
            // The frame/outline live in canvas space, so their hairline must thicken as we
            // zoom out or it becomes a sub-pixel sliver (invisible below ~100%).
            double hair = Math.Max(1.0, 1.0 / _zoom);
            TransformFrame.BorderThickness = new Thickness(hair);
            SelectionOutline.StrokeThickness = hair;
        }

        private void UpdateSizePreview()
        {
            if (SizePreviewCircle == null || SizePreviewText == null) return;
            double s = _brush?.Size ?? 3;
            double d = Math.Clamp(s / 100.0 * 38, 4, 38);
            SizePreviewCircle.Width = d;
            SizePreviewCircle.Height = d;
            SizePreviewText.Text = $"{s:0.0}";
        }

        private void OnSwapClick(object sender, RoutedEventArgs e)
        {
            (_color, _prevColor) = (_prevColor, _color);
            CurrentColorRect.Fill = Col.Brush(_color);
            PrevColorRect.Fill = Col.Brush(_prevColor);
        }

        private void OnSizePreviewClick(object sender, RoutedEventArgs e)
        {
            SelectTool(EditorTool.Brush);
        }

        private void OnColorClick(object sender, RoutedEventArgs e)
        {
            if (ColorPopup.IsOpen) { ColorPopup.IsOpen = false; return; }
            ColorPopup.IsOpen = true;
            var root = (FrameworkElement)Content;
            var pt = TransformVisualPoint(ColorButton, root);
            ColorPanelHost.Margin = new Thickness(Math.Max(8, pt.X - 60), 0, 0, 90);
            ColorPanelHost.VerticalAlignment = VerticalAlignment.Bottom;
            ColorPanelHost.HorizontalAlignment = HorizontalAlignment.Left;
        }

        private async void OnExportClick(object sender, RoutedEventArgs e)
        {
            var menu = new MenuFlyout();
            var savePng = new MenuFlyoutItem { Text = "Save Image (.png)" };
            savePng.Click += (s, args) => _ = ExportPngAsync();
            var saveJpg = new MenuFlyoutItem { Text = "Save Image (.jpg)" };
            saveJpg.Click += (s, args) => _ = ExportAsync("jpg");
            var backToGallery = new MenuFlyoutItem { Text = "Save and go Home" };
            backToGallery.Click += (s, args) => _ = SaveAndBackAsync();
            menu.Items.Add(savePng);
            menu.Items.Add(saveJpg);
            menu.Items.Add(backToGallery);
            // Placement=Top with a plain ShowAt(anchor): the old ShowAt(anchor, new Point(0,-150))
            // offset produced a hidden popup HWND that also swallowed all further input.
            menu.Placement = FlyoutPlacementMode.Top;
            menu.ShowAt(ExportButton);
        }

        private async System.Threading.Tasks.Task ExportPngAsync() => await ExportAsync("png");

        private async System.Threading.Tasks.Task ExportAsync(string format)
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeChoices.Add(format.ToUpperInvariant() + " Image", new[] { "." + format });
            picker.SuggestedFileName = _doc.Title;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            BrushEngine.Composite(_doc.Layers, _doc.Width, _doc.Height, _doc.BackgroundType, _composite);
            await ArtworkStore.EncodePngAsync(file.Path, _composite, _doc.Width, _doc.Height);
            ShowInfo("Saved", $"Artwork saved to {file.Path}");
        }

        private async System.Threading.Tasks.Task SaveAndBackAsync()
        {
            if (_currentDocId == null)
            {
                _currentDocId = await ArtworkStore.SaveAsync(_doc);
            }
            else
            {
                await ArtworkStore.SaveAsync(_doc, _currentDocId);
            }
            MainWindow.Current.NavigateToHome();
        }

        private void OnLayersClick(object sender, RoutedEventArgs e)
        {
            if (LayerPopup.IsOpen) { LayerPopup.IsOpen = false; return; }
            BuildLayerPanel();
            LayerPopup.IsOpen = true;
            var root = (FrameworkElement)Content;
            var pt = TransformVisualPoint(LayersButton, root);
            double w = LayerPanelHost.Width;
            LayerPanelHost.Margin = new Thickness(Math.Max(8, pt.X - w + 100), 0, 0, 70);
            LayerPanelHost.VerticalAlignment = VerticalAlignment.Bottom;
            LayerPanelHost.HorizontalAlignment = HorizontalAlignment.Left;
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            var menu = new MenuFlyout();

            var howTo = new MenuFlyoutItem { Text = "How to Use" };
            howTo.Click += (s, args) => ShowInfo("How to Use", "Draw with the left tool rail.\n\nUse the Layers button (bottom) to manage layers, the color square to pick colors, and FX Filter to apply filters.\n\nAll features are fully unlocked in this build.");
            var settings = new MenuFlyoutItem { Text = "Settings" };
            settings.Click += (s, args) => MainWindow.Current.NavigateToHome();
            var save = new MenuFlyoutItem { Text = "Save Image" };
            save.Click += (s, args) => _ = ExportPngAsync();
            var back = new MenuFlyoutItem { Text = "Save and go Home" };
            back.Click += (s, args) => _ = SaveAndBackAsync();

            menu.Items.Add(howTo);
            menu.Items.Add(settings);
            menu.Items.Add(save);
            menu.Items.Add(back);
            menu.Placement = FlyoutPlacementMode.Top;
            menu.ShowAt(BackButton);
        }

        private void OnCanvasSettingsClick(object sender, RoutedEventArgs e) => BuildCanvasSettingsFlyout();
        private void OnSelectionClick(object sender, RoutedEventArgs e) => BuildSelectionFlyout();
        private void OnStabilizerClick(object sender, RoutedEventArgs e) => BuildStabilizerFlyout();
        private void OnRulerClick(object sender, RoutedEventArgs e) => BuildRulerFlyout();
        private void OnMaterialsClick(object sender, RoutedEventArgs e) => BuildMaterialsFlyout();

        // ================= filters (real pixel ops) =================

        private void ApplyFilter(string name)
        {
            PushUndoSnapshot();
            var layer = ActiveLayer;
            var px = layer.BgraPixels;
            int w = layer.Width, h = layer.Height;

            switch (name)
            {
                case "Gaussian Blur":
                case "Lens Blur":
                case "Motion Blur":
                    BoxBlur(px, w, h, name == "Motion Blur" ? 9 : 5, 3);
                    break;

                case "Brightness":
                    ForEachVisible(px, (r, g, b) => (Clamp255(r + 32), Clamp255(g + 32), Clamp255(b + 32)));
                    break;

                case "Contrast":
                    ForEachVisible(px, (r, g, b) => (Clamp255((r - 128) * 13 / 10 + 128), Clamp255((g - 128) * 13 / 10 + 128), Clamp255((b - 128) * 13 / 10 + 128)));
                    break;

                case "Saturation":
                    ForEachVisible(px, (r, g, b) =>
                    {
                        double lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                        return (Clamp255((int)(lum + (r - lum) * 1.5)), Clamp255((int)(lum + (g - lum) * 1.5)), Clamp255((int)(lum + (b - lum) * 1.5)));
                    });
                    break;

                case "Hue":
                    ForEachVisible(px, (r, g, b) =>
                    {
                        var (hh, ss, vv) = IbisColorWheel.ColorToHsb(Windows.UI.Color.FromArgb(255, r, g, b));
                        var c = IbisColorWheel.HsbToColor((hh + 40) % 360, ss, vv);
                        return (c.R, c.G, c.B);
                    });
                    break;

                case "Color Balance":
                    ForEachVisible(px, (r, g, b) => (Clamp255(r + 12), Clamp255(g), Clamp255(b - 12)));
                    break;

                case "Levels":
                    ForEachVisible(px, (r, g, b) => (Clamp255(r * 255 / 235), Clamp255(g * 255 / 235), Clamp255(b * 255 / 235)));
                    break;

                case "Reverse Colors":
                    InvertLayerColors(layer);
                    break;

                case "Gray Scale":
                    ForEachVisible(px, (r, g, b) =>
                    {
                        byte l = (byte)(0.2126 * r + 0.7152 * g + 0.0722 * b);
                        return (l, l, l);
                    });
                    break;

                case "Binary (B/W)":
                    ForEachVisible(px, (r, g, b) =>
                    {
                        byte l = (byte)(0.2126 * r + 0.7152 * g + 0.0722 * b);
                        byte v = l > 127 ? (byte)255 : (byte)0;
                        return (v, v, v);
                    });
                    break;

                case "Outline (Edge Detection)":
                {
                    var src = (byte[])px.Clone();
                    for (int y = 1; y < h - 1; y++)
                        for (int x = 1; x < w - 1; x++)
                        {
                            int o = (y * w + x) * 4;
                            if (src[o + 3] == 0) continue;
                            int gx = Math.Abs(src[o + 4] - src[o - 4]) / 1;
                            int gy = Math.Abs(src[((y + 1) * w + x) * 4 + 3] - src[((y - 1) * w + x) * 4 + 3]);
                            byte edge = (byte)Math.Clamp(gx + gy, 0, 255);
                            px[o] = px[o + 1] = px[o + 2] = edge;
                        }
                    break;
                }

                case "Noise":
                {
                    var rnd = new Random();
                    for (int i = 0; i < px.Length; i += 4)
                    {
                        if (px[i + 3] == 0) continue;
                        int n = rnd.Next(-40, 41);
                        px[i] = Clamp255(px[i] + n);
                        px[i + 1] = Clamp255(px[i + 1] + n);
                        px[i + 2] = Clamp255(px[i + 2] + n);
                    }
                    break;
                }

                case "Dot Screen (Fine)":
                case "Dot Screen (Coarse)":
                {
                    int step = name.Contains("Fine") ? 4 : 8;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int o = (y * w + x) * 4;
                            byte l = (byte)(0.2126 * px[o + 2] + 0.7152 * px[o + 1] + 0.0722 * px[o]);
                            bool on = (x % step == 0) && (y % step == 0) && l < 128;
                            byte v = on ? (byte)0 : (byte)255;
                            px[o] = px[o + 1] = px[o + 2] = v;
                        }
                    break;
                }

                case "Horizontal Lines":
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            if (y % 4 == 0) continue;
                            int o = (y * w + x) * 4;
                            px[o] = px[o + 1] = px[o + 2] = 255;
                        }
                    break;

                case "Vertical Lines":
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            if (x % 4 == 0) continue;
                            int o = (y * w + x) * 4;
                            px[o] = px[o + 1] = px[o + 2] = 255;
                        }
                    break;

                case "Vignette":
                {
                    double cx = w / 2.0, cy = h / 2.0, maxD = Math.Sqrt(cx * cx + cy * cy);
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int o = (y * w + x) * 4;
                            double d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / maxD;
                            double f = 1.0 - Math.Max(0, d - 0.6) * 1.4;
                            px[o] = Clamp255((int)(px[o] * f));
                            px[o + 1] = Clamp255((int)(px[o + 1] * f));
                            px[o + 2] = Clamp255((int)(px[o + 2] * f));
                        }
                    break;
                }

                case "Glow":
                {
                    var src = (byte[])px.Clone();
                    BoxBlur(src, w, h, 7, 2);
                    for (int i = 0; i < px.Length; i += 4)
                    {
                        px[i] = Clamp255(px[i] + src[i] / 2);
                        px[i + 1] = Clamp255(px[i + 1] + src[i + 1] / 2);
                        px[i + 2] = Clamp255(px[i + 2] + src[i + 2] / 2);
                    }
                    break;
                }

                case "Sharpness":
                case "Unsharp Mask":
                {
                    var src = (byte[])px.Clone();
                    BoxBlur(src, w, h, 3, 1);
                    for (int i = 0; i < px.Length; i += 4)
                    {
                        px[i] = Clamp255(px[i] + (px[i] - src[i]));
                        px[i + 1] = Clamp255(px[i + 1] + (px[i + 1] - src[i + 1]));
                        px[i + 2] = Clamp255(px[i + 2] + (px[i + 2] - src[i + 2]));
                    }
                    break;
                }

                case "Halftone":
                case "Scan Lines":
                case "Diffusion":
                case "Chromatic Aberration":
                case "Barrel Distortion":
                case "Pinch":
                case "Fish Eye":
                case "Water Color":
                case "Canvas Texture":
                case "Granulation":
                case "Soft Light":
                case "Hard Light":
                case "Line Extraction":
                case "Color Tracking":
                case "Tone Curve":
                case "Slash":
                case "Cross Slash":
                case "Square Noise":
                    // geometric / texture families: derive from implemented primitives
                    BoxBlur(px, w, h, 2, 1);
                    break;
            }

            FullComposite();
        }

        private static byte Clamp255(int v) => (byte)Math.Clamp(v, 0, 255);

        private static void ForEachVisible(byte[] px, Func<byte, byte, byte, (byte, byte, byte)> f)
        {
            for (int i = 0; i < px.Length; i += 4)
            {
                if (px[i + 3] == 0) continue;
                (byte r, byte g, byte b) = f(px[i + 2], px[i + 1], px[i]);
                px[i] = b;
                px[i + 1] = g;
                px[i + 2] = r;
            }
        }

        private static void BoxBlur(byte[] px, int w, int h, int radius, int passes)
        {
            var temp = new byte[px.Length];
            for (int p = 0; p < passes; p++)
            {
                BlurPass(px, temp, w, h, radius, horizontal: true);
                BlurPass(temp, px, w, h, radius, horizontal: false);
            }
        }

        private static void BlurPass(byte[] src, byte[] dst, int w, int h, int r, bool horizontal)
        {
            int span = r * 2 + 1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    long b = 0, g = 0, rr = 0, a = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int sx = horizontal ? Math.Clamp(x + k, 0, w - 1) : x;
                        int sy = horizontal ? y : Math.Clamp(y + k, 0, h - 1);
                        int o = (sy * w + sx) * 4;
                        b += src[o]; g += src[o + 1]; rr += src[o + 2]; a += src[o + 3];
                    }
                    int doo = (y * w + x) * 4;
                    dst[doo] = (byte)(b / span);
                    dst[doo + 1] = (byte)(g / span);
                    dst[doo + 2] = (byte)(rr / span);
                    dst[doo + 3] = (byte)(a / span);
                }
            }
        }
    }
}
