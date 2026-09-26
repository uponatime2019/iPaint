using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;

namespace iPaint.Controls
{
    /// <summary>The ibisPaint word mark: "ibis" + 8-sector color wheel + "aint".</summary>
    public sealed partial class IbisLogo : UserControl
    {
        public static readonly DependencyProperty WheelSizeProperty =
            DependencyProperty.Register(nameof(WheelSize), typeof(double), typeof(IbisLogo),
                new PropertyMetadata(44.0, (d, e) => ((IbisLogo)d).Render()));

        public double WheelSize
        {
            get => (double)GetValue(WheelSizeProperty);
            set => SetValue(WheelSizeProperty, value);
        }

        public IbisLogo()
        {
            InitializeComponent();
            Loaded += (s, e) => Render();
        }

        private void Render()
        {
            double size = WheelSize;
            LeftText.FontSize = size * 1.32;
            RightText.FontSize = size * 1.32;
            WheelCanvas.Width = size;
            WheelCanvas.Height = size;
            WheelCanvas.Children.Clear();

            double r = size / 2.0;
            double cx = r, cy = r;
            var colors = new[]
            {
                "#FF0000", "#FF7F00", "#FFFF00", "#7FFF00",
                "#00FF55", "#00FFFF", "#0055FF", "#8B00FF"
            };

            for (int i = 0; i < 8; i++)
            {
                double a0 = Math.PI * 2 * i / 8 - Math.PI / 2;
                double a1 = Math.PI * 2 * (i + 1) / 8 - Math.PI / 2;
                double x0 = cx + r * Math.Cos(a0), y0 = cy + r * Math.Sin(a0);
                double x1 = cx + r * Math.Cos(a1), y1 = cy + r * Math.Sin(a1);
                var segs = new PathSegmentCollection
                {
                    new LineSegment { Point = new Windows.Foundation.Point(x0, y0) },
                    new ArcSegment
                    {
                        Point = new Windows.Foundation.Point(x1, y1),
                        Size = new Windows.Foundation.Size(r, r),
                        IsLargeArc = false,
                        SweepDirection = SweepDirection.Clockwise
                    }
                };
                var fig = new PathFigure { StartPoint = new Windows.Foundation.Point(cx, cy), Segments = segs, IsClosed = true };
                var geo = new PathGeometry { Figures = new PathFigureCollection { fig } };
                WheelCanvas.Children.Add(new Path
                {
                    Data = geo,
                    Fill = new SolidColorBrush(ParseHex(colors[i]))
                });
            }

            // White core with a thin ring
            double core = r * 0.52;
            WheelCanvas.Children.Add(new Ellipse
            {
                Width = core * 2,
                Height = core * 2,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.White),
                Margin = new Thickness(cx - core, cy - core, 0, 0)
            });
        }

        private static Windows.UI.Color ParseHex(string hex)
        {
            hex = hex.TrimStart('#');
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return Windows.UI.Color.FromArgb(255, r, g, b);
        }
    }
}
