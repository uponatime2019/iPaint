using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using Windows.Foundation;

namespace iPaint.Controls
{
    /// <summary>HSB color wheel: hue ring + inner saturation/value diamond, like the original.</summary>
    public sealed class IbisColorWheel : Grid
    {
        public double Hue { get; private set; } = 0;
        public double Saturation { get; private set; } = 0;
        public double Brightness { get; private set; } = 0;

        public event Action? ColorChanged;

        private readonly Canvas _canvas = new();
        private bool _isDown;
        private bool _dragRing;
        private bool _dragDiamond;

        public IbisColorWheel()
        {
            Children.Add(_canvas);
            SizeChanged += (s, e) => Render();
            Loaded += (s, e) => Render();
            PointerPressed += OnPointer;
            PointerMoved += OnPointerMove;
            PointerReleased += OnPointerUp;
            PointerCaptureLost += OnPointerUp;
        }

        public void SetColor(double h, double s, double v)
        {
            Hue = h;
            Saturation = s;
            Brightness = v;
            Render();
        }

        private void OnPointer(object sender, PointerRoutedEventArgs e)
        {
            _isDown = true;
            _dragRing = _dragDiamond = false;
            CapturePointer(e.Pointer);
            Classify(e.GetCurrentPoint(_canvas).Position);
        }

        private void OnPointerMove(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDown) return;
            Classify(e.GetCurrentPoint(_canvas).Position);
        }

        private void OnPointerUp(object sender, PointerRoutedEventArgs e)
        {
            _isDown = false;
            _dragRing = _dragDiamond = false;
        }

        private void Classify(Point p)
        {
            double size = Math.Min(ActualWidth, ActualHeight);
            if (size < 10) return;
            double c = size / 2;
            double dx = p.X - c, dy = p.Y - c;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double r = c - 2;
            double innerR = r * 0.72;
            double half = innerR / Math.Sqrt(2);

            if (!_dragDiamond && (dist >= innerR - 4 || _dragRing))
            {
                _dragRing = true;
                double deg = Math.Atan2(dy, dx) * 180 / Math.PI + 90;
                if (deg < 0) deg += 360;
                Hue = deg;
            }
            else
            {
                _dragDiamond = true;
                double scale = half / Math.Sqrt(2);
                double u = ((dx / scale) + (dy / scale)) / 2;
                double w = ((dy / scale) - (dx / scale)) / 2;
                Saturation = Math.Clamp((u + 1) / 2, 0, 1);
                Brightness = Math.Clamp((1 - w) / 2, 0, 1);
            }
            ColorChanged?.Invoke();
            Render();
        }

        private void Render()
        {
            double size = Math.Min(ActualWidth, ActualHeight);
            if (size < 10) return;
            _canvas.Children.Clear();
            _canvas.Width = _canvas.Height = size;
            double c = size / 2;
            double r = c - 2;
            double innerR = r * 0.72;

            // Hue ring as 36 sectors
            const int sectors = 36;
            for (int i = 0; i < sectors; i++)
            {
                double a0 = Math.PI * 2 * i / sectors - Math.PI / 2;
                double a1 = Math.PI * 2 * (i + 1) / sectors - Math.PI / 2;
                var fig = new PathFigure { StartPoint = new Point(c + r * Math.Cos(a0), c + r * Math.Sin(a0)) };
                fig.Segments.Add(new ArcSegment
                {
                    Point = new Point(c + r * Math.Cos(a1), c + r * Math.Sin(a1)),
                    Size = new Size(r, r),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = false
                });
                fig.Segments.Add(new LineSegment { Point = new Point(c + innerR * Math.Cos(a1), c + innerR * Math.Sin(a1)) });
                fig.Segments.Add(new ArcSegment
                {
                    Point = new Point(c + innerR * Math.Cos(a0), c + innerR * Math.Sin(a0)),
                    Size = new Size(innerR, innerR),
                    SweepDirection = SweepDirection.Counterclockwise
                });
                fig.IsClosed = true;
                _canvas.Children.Add(new Path
                {
                    Data = new PathGeometry { Figures = { fig } },
                    Fill = new SolidColorBrush(HsbToColor(360.0 * (i + 0.5) / sectors, 1, 1))
                });
            }

            // Inner diamond: grid rotated 45° with S gradient + V gradient overlay
            double half = innerR / Math.Sqrt(2);
            var diamond = new Grid
            {
                Width = half * 2,
                Height = half * 2,
                RenderTransform = new RotateTransform { Angle = 45, CenterX = half, CenterY = half },
                Margin = new Thickness(c - half, c - half, 0, 0)
            };
            var hueColor = HsbToColor(Hue, 1, 1);
            diamond.Children.Add(new Rectangle
            {
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0.5),
                    EndPoint = new Point(1, 0.5),
                    GradientStops =
                    {
                        new GradientStop { Color = Microsoft.UI.Colors.White, Offset = 0 },
                        new GradientStop { Color = hueColor, Offset = 1 }
                    }
                }
            });
            diamond.Children.Add(new Rectangle
            {
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = Microsoft.UI.Colors.Transparent, Offset = 0 },
                        new GradientStop { Color = Microsoft.UI.Colors.Black, Offset = 1 }
                    }
                }
            });
            diamond.Children.Add(new Rectangle
            {
                Stroke = new SolidColorBrush(Microsoft.UI.Colors.Black),
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            });
            _canvas.Children.Add(diamond);

            // Ring thumb
            double rad = Hue * Math.PI / 180 - Math.PI / 2;
            double tx = c + (r + innerR) / 2 * Math.Cos(rad);
            double ty = c + (r + innerR) / 2 * Math.Sin(rad);
            _canvas.Children.Add(new Ellipse
            {
                Width = 16,
                Height = 16,
                Fill = new SolidColorBrush(HsbToColor(Hue, 1, 1)),
                Stroke = new SolidColorBrush(Microsoft.UI.Colors.White),
                StrokeThickness = 2,
                Margin = new Thickness(tx - 8, ty - 8, 0, 0)
            });

            // Diamond thumb (inverse of the picking transform)
            double u = Saturation * 2 - 1;
            double w = (1 - Brightness) * 2 - 1;
            double scale = half / Math.Sqrt(2);
            double px = c + (u - w) * scale;
            double py = c + (u + w) * scale;
            _canvas.Children.Add(new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(HsbToColor(Hue, Math.Max(Saturation, 0.01), Math.Max(Brightness, 0.01))),
                Stroke = new SolidColorBrush(Microsoft.UI.Colors.White),
                StrokeThickness = 2,
                Margin = new Thickness(px - 7, py - 7, 0, 0)
            });
        }

        public static Windows.UI.Color HsbToColor(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;
            (double r, double g, double b) = (h / 60) switch
            {
                < 1 => (c, x, 0.0),
                < 2 => (x, c, 0.0),
                < 3 => (0.0, c, x),
                < 4 => (0.0, x, c),
                < 5 => (x, 0.0, c),
                _ => (c, 0.0, x)
            };
            return Windows.UI.Color.FromArgb(255,
                (byte)Math.Clamp((r + m) * 255, 0, 255),
                (byte)Math.Clamp((g + m) * 255, 0, 255),
                (byte)Math.Clamp((b + m) * 255, 0, 255));
        }

        public static (double h, double s, double v) ColorToHsb(Windows.UI.Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double d = max - min;
            double h = 0;
            if (d > 0)
            {
                if (max == r) h = 60 * (((g - b) / d) % 6);
                else if (max == g) h = 60 * ((b - r) / d + 2);
                else h = 60 * ((r - g) / d + 4);
            }
            if (h < 0) h += 360;
            double s = max == 0 ? 0 : d / max;
            return (h, s, max);
        }
    }
}
