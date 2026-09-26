using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Windows.Storage;

namespace iPaint.Services
{
    /// <summary>App settings persisted as JSON (mirrors the original app's Settings flyout).</summary>
    public class IbisSettings
    {
        public bool ConsentAnalytics { get; set; }
        public bool OnboardingDone { get; set; }

        // Gesture
        public bool UndoTwoFingerTap { get; set; } = true;
        public bool RedoThreeFingerTap { get; set; } = true;
        public bool QuickEyedropper { get; set; } = true;
        public bool CustomizeEyedropperTime { get; set; }
        public double EyedropperActivationSeconds { get; set; } = 0.50;
        public bool ViewRotation { get; set; } = true;

        // User Interface
        public bool ShowToolLabels { get; set; } = true;
        public bool ConfirmBackFromCanvas { get; set; } = true;
        public bool ShowTutorials { get; set; } = true;

        // Canvas
        public bool UsePressureSensitivity { get; set; } = true;
        public double StabilizerValue { get; set; } = 0;
        public string StabilizerMethod { get; set; } = "Real Time";
        public bool ReferenceWindow { get; set; }
        public bool Grid { get; set; }
        public int GridDivisions { get; set; } = 3;
        public bool MangaManuscript { get; set; }
        public string DisplayWhenZoomed { get; set; } = "Smooth";

        public static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "iPaint");
                return Path.Combine(dir, "settings.json");
            }
        }

        public static IbisSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonConvert.DeserializeObject<IbisSettings>(File.ReadAllText(SettingsPath)) ?? new IbisSettings();
            }
            catch { }
            return new IbisSettings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }

    /// <summary>The built-in brush catalog shown in the Brush panel (Basic tab).</summary>
    public static class BrushCatalog
    {
        public class Entry
        {
            public string Name { get; set; }
            public string Category { get; set; }
            public double Size { get; set; }
            public double Softness { get; set; }
            public double Spacing { get; set; }
            public double Opacity { get; set; } = 1.0;
            public string Kind { get; set; } = "Round"; // Round, Airbrush, Textured, Eraser
            public Entry(string name, string category, double size, double softness, double spacing, string kind = "Round", double opacity = 1.0)
            { Name = name; Category = category; Size = size; Softness = softness; Spacing = spacing; Kind = kind; Opacity = opacity; }
        }

        public static readonly List<Entry> Basic = new()
        {
            new("Dip Pen (Soft)", "Simple", 3.0, 0.10, 0.06),
            new("Dip Pen (Hard)", "Simple", 3.0, 0.02, 0.05),
            new("Dip Pen (Bleed)", "Simple", 3.0, 0.45, 0.07),
            new("Felt Tip Pen (Soft)", "Simple", 8.0, 0.25, 0.07),
            new("Felt Tip Pen (Hard)", "Simple", 8.0, 0.08, 0.07),
            new("Pen (Fade)", "Simple", 48.0, 0.55, 0.05),
            new("Impasto Brush - Flat", "Simple", 48.0, 0.30, 0.05),
            new("Digital Pen", "Simple", 8.0, 0.12, 0.06),
            new("Pencil (#1)", "Sketch", 4.0, 0.50, 0.10, "Textured"),
            new("Pencil (#2)", "Sketch", 6.0, 0.55, 0.10, "Textured"),
            new("Pencil (Graphite)", "Sketch", 8.0, 0.60, 0.09, "Textured"),
            new("Crayon", "Sketch", 12.0, 0.70, 0.12, "Textured"),
            new("Charcoal", "Sketch", 16.0, 0.75, 0.12, "Textured"),
            new("Oil (Rough)", "Comic", 20.0, 0.45, 0.08, "Textured"),
            new("Oil (Hair)", "Comic", 14.0, 0.40, 0.06, "Textured"),
            new("Flat Brush (Point)", "Comic", 16.0, 0.20, 0.05),
            new("Flat Brush (Normal)", "Comic", 20.0, 0.25, 0.05),
            new("Flat Brush (Fan)", "Comic", 24.0, 0.35, 0.06),
            new("Marker (Hard)", "Ink", 10.0, 0.02, 0.05),
            new("Marker (Soft)", "Ink", 10.0, 0.30, 0.06),
            new("Ink Pen", "Ink", 2.0, 0.02, 0.04),
            new("Airbrush (Normal)", "Airbrush", 48.0, 0.95, 0.04, "Airbrush"),
            new("Airbrush (Triangle)", "Airbrush", 48.0, 0.90, 0.04, "Airbrush"),
            new("Airbrush (Particle)", "Airbrush", 48.0, 0.85, 0.30, "Airbrush"),
            new("Eraser (Hard)", "Eraser", 12.0, 0.02, 0.05, "Eraser"),
            new("Eraser (Soft)", "Eraser", 24.0, 0.60, 0.05, "Eraser"),
        };

        public static List<Entry> ByCategory(string category)
        {
            if (category == "All") return Basic;
            return Basic.FindAll(b => b.Category == category);
        }
    }

    /// <summary>The built-in FX filter list (84 filters in the original; core set here, all unlocked).</summary>
    public static class FilterCatalog
    {
        public static readonly string[] Filters =
        {
            "Gaussian Blur", "Motion Blur", "Lens Blur", "Sharpness", "Unsharp Mask",
            "Brightness", "Contrast", "Saturation", "Hue", "Color Balance",
            "Levels", "Tone Curve", "Color Tracking", "Reverse Colors", "Gray Scale",
            "Binary (B/W)", "Line Extraction", "Outline (Edge Detection)", "Noise", "Granulation",
            "Dot Screen (Fine)", "Dot Screen (Coarse)", "Horizontal Lines", "Vertical Lines", "Slash",
            "Cross Slash", "Square Noise", "Soft Light", "Hard Light", "Glow",
            "Diffusion", "Chromatic Aberration", "Barrel Distortion", "Pinch", "Fish Eye",
            "Vignette", "Scan Lines", "Halftone", "Water Color", "Canvas Texture",
        };
    }
}
