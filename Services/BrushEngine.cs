using System;
using System.Collections.Generic;
using iPaint.Models;
using Windows.UI;

namespace iPaint.Services
{
    public class BrushSettings
    {
        public string Name { get; set; } = "Dip Pen (Hard)";
        public double Size { get; set; } = 3.0;             // px
        public double Opacity { get; set; } = 1.0;          // 0..1
        public double StartThickness { get; set; } = 0.0;   // %
        public double EndThickness { get; set; } = 0.0;     // %
        public double Softness { get; set; } = 0.25;        // 0=hard edge .. 1=airbrush
        public double Spacing { get; set; } = 0.08;         // fraction of size
        public bool IsEraser { get; set; }
        public double MinSizeRatio { get; set; } = 0.15;    // pressure-simulated min size
    }

    /// <summary>Software raster engine: stamp-based strokes + per-pixel layer compositing.</summary>
    public static class BrushEngine
    {
        public static void Composite(List<PaintLayer> layers, int width, int height, string bgType, byte[] outputBgra)
        {
            CompositeRegion(layers, width, height, bgType, outputBgra, 0, 0, width, height);
        }

        /// <summary>Composites only a region of the document into the matching region of outputBgra.</summary>
        public static void CompositeRegion(List<PaintLayer> layers, int width, int height, string bgType,
            byte[] outputBgra, int rx, int ry, int rw, int rh)
        {
            rx = Math.Max(0, rx); ry = Math.Max(0, ry);
            rw = Math.Min(width - rx, rw); rh = Math.Min(height - ry, rh);
            if (rw <= 0 || rh <= 0) return;

            bool bgTransparent = bgType == "Transparent";
            bool bgDark = bgType == "Dark";

            for (int y = ry; y < ry + rh; y++)
            {
                int rowBase = y * width;
                for (int x = rx; x < rx + rw; x++)
                {
                    int o = (rowBase + x) * 4;
                    double dB, dG, dR, dA;
                    if (bgTransparent)
                    {
                        dB = dG = dR = 0; dA = 0.0;
                    }
                    else if (bgDark)
                    {
                        dB = 0x1E; dG = 0x1E; dR = 0x1E; dA = 1.0;
                    }
                    else
                    {
                        dB = dG = dR = 255; dA = 1.0;
                    }

                    PaintLayer? clipBase = null;
                    for (int li = 0; li < layers.Count; li++)
                    {
                        var layer = layers[li];
                        bool isClipBase = false;
                        if (clipBase == layer) { isClipBase = false; }  // clipped drawing applies against clipBase
                        if (!layer.IsVisible || layer.Opacity <= 0)
                        {
                            if (layer.IsBackground) { }
                            continue;
                        }
                        if (layer.IsClipping)
                        {
                            // clipped to the nearest visible layer below
                            if (clipBase == null) continue;
                        }
                        else
                        {
                            clipBase = layer;
                        }

                        byte[] px = layer.BgraPixels;
                        int so = (rowBase + x) * 4;
                        byte sAraw = px[so + 3];
                        if (sAraw == 0) continue;

                        double sA = (sAraw / 255.0) * layer.Opacity;
                        if (sA <= 0.001) continue;

                        double sB = px[so], sG = px[so + 1], sR = px[so + 2];

                        switch (layer.BlendMode)
                        {
                            case "Multiply": sB = sB * dB / 255; sG = sG * dG / 255; sR = sR * dR / 255; break;
                            case "Screen": sB = 255 - (255 - sB) * (255 - dB) / 255; sG = 255 - (255 - sG) * (255 - dG) / 255; sR = 255 - (255 - sR) * (255 - dR) / 255; break;
                            case "Add": sB = Math.Min(255, sB + dB); sG = Math.Min(255, sG + dG); sR = Math.Min(255, sR + dR); break;
                            case "Overlay":
                                sB = dB < 128 ? 2 * sB * dB / 255 : 255 - 2 * (255 - sB) * (255 - dB) / 255;
                                sG = dG < 128 ? 2 * sG * dG / 255 : 255 - 2 * (255 - sG) * (255 - dG) / 255;
                                sR = dR < 128 ? 2 * sR * dR / 255 : 255 - 2 * (255 - sR) * (255 - dR) / 255;
                                break;
                        }

                        double outA = sA + dA * (1.0 - sA);
                        if (outA <= 0) continue;
                        dB = (sB * sA + dB * dA * (1 - sA)) / outA;
                        dG = (sG * sA + dG * dA * (1 - sA)) / outA;
                        dR = (sR * sA + dR * dA * (1 - sA)) / outA;
                        dA = outA;
                    }

                    outputBgra[o] = (byte)Math.Clamp(dB, 0, 255);
                    outputBgra[o + 1] = (byte)Math.Clamp(dG, 0, 255);
                    outputBgra[o + 2] = (byte)Math.Clamp(dR, 0, 255);
                    outputBgra[o + 3] = (byte)Math.Clamp(dA * 255, 0, 255);
                }
            }
        }

        /// <summary>Draws one stroke segment into the layer. Uses spacing-stamped radial falloff.</summary>
        public static void DrawSegment(PaintLayer layer, double x0, double y0, double x1, double y1,
            in Color color, BrushSettings brush, double pressureStart, double pressureEnd, double progress)
        {
            double size = brush.Size;
            // Taper simulation using start/end thickness + stroke progress
            double taper = 1.0;
            if (brush.StartThickness > 0 && progress < 0.25)
                taper = 1.0 - (brush.StartThickness / 100.0) * (1.0 - progress * 4.0);
            if (brush.EndThickness > 0 && progress > 0.75)
                taper = 1.0 - (brush.EndThickness / 100.0) * ((progress - 0.75) * 4.0);
            double pressure = pressureStart + (pressureEnd - pressureStart) * progress;
            double effSize = Math.Max(0.7, size * taper * (0.5 + pressure * 0.5));

            double dx = x1 - x0, dy = y1 - y0;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double step = Math.Max(0.7, effSize * brush.Spacing);
            int steps = Math.Max(1, (int)Math.Ceiling(dist / step));

            for (int i = 0; i <= steps; i++)
            {
                double t = steps == 0 ? 0 : (double)i / steps;
                double sx = x0 + dx * t, sy = y0 + dy * t;
                Stamp(layer, sx, sy, effSize, color, brush);
            }
        }

        /// <summary>Stamps one soft/hard round dab into the layer buffer.</summary>
        public static void Stamp(PaintLayer layer, double cx, double cy, double size, in Color color, BrushSettings brush)
        {
            int w = layer.Width, h = layer.Height;
            byte[] px = layer.BgraPixels;
            double radius = size / 2.0;
            if (radius < 0.35) radius = 0.35;
            int x0 = Math.Max(0, (int)Math.Floor(cx - radius));
            int x1 = Math.Min(w - 1, (int)Math.Ceiling(cx + radius));
            int y0 = Math.Max(0, (int)Math.Floor(cy - radius));
            int y1 = Math.Min(h - 1, (int)Math.Ceiling(cy + radius));
            if (x1 < x0 || y1 < y0) return;

            double inner = radius * (1.0 - Math.Clamp(brush.Softness, 0, 0.98));
            double alphaScale = brush.Opacity;

            for (int y = y0; y <= y1; y++)
            {
                double ddy = y - cy;
                for (int x = x0; x <= x1; x++)
                {
                    double ddx = x - cx;
                    double d = Math.Sqrt(ddx * ddx + ddy * ddy);
                    if (d > radius) continue;
                    double a;
                    if (d <= inner || inner >= radius) a = 1.0;
                    else a = 1.0 - (d - inner) / (radius - inner);
                    a = a * a * (3 - 2 * a); // smoothstep
                    a *= alphaScale;
                    if (a <= 0.002) continue;

                    int o = (y * w + x) * 4;
                    byte sA = (byte)Math.Round(Math.Clamp(a, 0, 1) * 255);

                    if (brush.IsEraser)
                    {
                        int ea = px[o + 3] - sA;
                        px[o + 3] = (byte)Math.Clamp(ea, 0, 255);
                    }
                    else if (layer.AlphaLock)
                    {
                        int dstA = px[o + 3];
                        if (dstA == 0) continue;
                        px[o] = color.B; px[o + 1] = color.G; px[o + 2] = color.R;
                    }
                    else
                    {
                        double sa = sA / 255.0;
                        double da = px[o + 3] / 255.0;
                        double outA = sa + da * (1 - sa);
                        if (outA <= 0) { px[o + 3] = 0; continue; }
                        px[o] = (byte)Math.Clamp((color.B * sa + px[o] * da * (1 - sa)) / outA, 0, 255);
                        px[o + 1] = (byte)Math.Clamp((color.G * sa + px[o + 1] * da * (1 - sa)) / outA, 0, 255);
                        px[o + 2] = (byte)Math.Clamp((color.R * sa + px[o + 2] * da * (1 - sa)) / outA, 0, 255);
                        px[o + 3] = (byte)Math.Clamp(outA * 255, 0, 255);
                    }
                }
            }
        }

        /// <summary>Flood fill with tolerance on the given layer (bucket tool).</summary>
        public static void FloodFill(PaintLayer layer, int sx, int sy, in Color color, double tolerance)
        {
            int w = layer.Width, h = layer.Height;
            byte[] px = layer.BgraPixels;
            if (sx < 0 || sy < 0 || sx >= w || sy >= h) return;
            int start = (sy * w + sx) * 4;
            byte tB = px[start], tG = px[start + 1], tR = px[start + 2], tA = px[start + 3];
            int tol = (int)(tolerance * 255);

            bool Matches(int o)
            {
                return Math.Abs(px[o] - tB) <= tol && Math.Abs(px[o + 1] - tG) <= tol &&
                       Math.Abs(px[o + 2] - tR) <= tol && Math.Abs(px[o + 3] - tA) <= tol;
            }

            var stack = new Stack<int>();
            stack.Push(sy * w + sx);
            var visited = new bool[w * h];
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                if (visited[p]) continue;
                visited[p] = true;
                int x = p % w, y = p / w;
                int o = p * 4;
                if (!Matches(o)) continue;
                px[o] = color.B; px[o + 1] = color.G; px[o + 2] = color.R; px[o + 3] = 255;
                if (x > 0) stack.Push(p - 1);
                if (x < w - 1) stack.Push(p + 1);
                if (y > 0) stack.Push(p - w);
                if (y < h - 1) stack.Push(p + w);
            }
        }

        public static void ClearLayer(PaintLayer layer)
            => Array.Clear(layer.BgraPixels, 0, layer.BgraPixels.Length);

        public static void FlipLayer(PaintLayer layer, bool horizontal)
        {
            int w = layer.Width, h = layer.Height;
            byte[] src = layer.BgraPixels;
            byte[] dst = new byte[src.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = horizontal ? w - 1 - x : x;
                    int sy = !horizontal ? h - 1 - y : y;
                    int so = (sy * w + sx) * 4, doo = (y * w + x) * 4;
                    dst[doo] = src[so]; dst[doo + 1] = src[so + 1]; dst[doo + 2] = src[so + 2]; dst[doo + 3] = src[so + 3];
                }
            Array.Copy(dst, src, src.Length);
        }
    }
}
