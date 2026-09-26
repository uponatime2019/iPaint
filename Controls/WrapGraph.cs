using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Foundation;

namespace iPaint.Controls
{
    /// <summary>A minimal wrap panel (children flow left→right, wrap to next line).</summary>
    public class WrapGraph : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            double x = 0, y = 0, rowHeight = 0;
            double maxW = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;

            foreach (var child in Children)
            {
                child.Measure(new Size(maxW, availableSize.Height));
                var sz = child.DesiredSize;
                if (x + sz.Width > maxW && x > 0)
                {
                    x = 0;
                    y += rowHeight;
                    rowHeight = 0;
                }
                x += sz.Width;
                rowHeight = Math.Max(rowHeight, sz.Height);
            }
            return new Size(maxW, y + rowHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double x = 0, y = 0, rowHeight = 0;
            foreach (var child in Children)
            {
                var sz = child.DesiredSize;
                if (x + sz.Width > finalSize.Width && x > 0)
                {
                    x = 0;
                    y += rowHeight;
                    rowHeight = 0;
                }
                child.Arrange(new Rect(x, y, sz.Width, sz.Height));
                x += sz.Width;
                rowHeight = Math.Max(rowHeight, sz.Height);
            }
            return finalSize;
        }
    }
}
