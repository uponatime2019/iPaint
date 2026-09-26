using System;
using System.Collections.Generic;
using System.Linq;

namespace iPaint.Models
{
    /// <summary>A single raster layer of an artwork (straight BGRA pixel buffer).</summary>
    public class PaintLayer
    {
        public string Name { get; set; } = "1";
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsVisible { get; set; } = true;
        public double Opacity { get; set; } = 1.0;          // 0..1
        public string BlendMode { get; set; } = "Normal";   // Normal, Multiply, Add, Screen, Overlay, LinearBurn(Subtract)
        public bool AlphaLock { get; set; }
        public bool IsClipping { get; set; }
        public bool IsBackground { get; set; }

        public byte[] BgraPixels { get; set; }

        public PaintLayer() { }

        public PaintLayer(int width, int height, bool filledWhite)
        {
            Width = width;
            Height = height;
            BgraPixels = new byte[width * height * 4];
            if (filledWhite)
            {
                for (int i = 0; i < BgraPixels.Length; i += 4)
                {
                    BgraPixels[i] = 255;
                    BgraPixels[i + 1] = 255;
                    BgraPixels[i + 2] = 255;
                    BgraPixels[i + 3] = 255;
                }
            }
        }

        public PaintLayer Clone()
        {
            return new PaintLayer
            {
                Name = Name,
                Width = Width,
                Height = Height,
                IsVisible = IsVisible,
                Opacity = Opacity,
                BlendMode = BlendMode,
                AlphaLock = AlphaLock,
                IsClipping = IsClipping,
                IsBackground = IsBackground,
                BgraPixels = (byte[])BgraPixels.Clone()
            };
        }
    }

    /// <summary>An open/working artwork document.</summary>
    public class ArtworkDocument
    {
        public string Title { get; set; } = "Untitled1";
        public int Width { get; set; }
        public int Height { get; set; }
        public string BackgroundType { get; set; } = "White"; // White, Transparent, Dark
        public List<PaintLayer> Layers { get; set; } = new();
        public long EditingTimeMs { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int LayerCount => Layers.Count(l => !l.IsBackground);

        private static int _untitledCounter = 0;
        public static string NextUntitledName() => $"Untitled{++_untitledCounter}";
    }

    /// <summary>Snapshot for the undo stack (full layer bitmap copies — simple and reliable).</summary>
    public class DocumentSnapshot
    {
        public List<PaintLayer> Layers { get; set; }
        public string Title { get; set; }
        public string BackgroundType { get; set; }
    }
}
