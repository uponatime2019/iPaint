using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using iPaint.Controls;
using iPaint.Models;
using iPaint.Services;
using WinUI = Microsoft.UI.Xaml;

namespace iPaint.Views
{
    public static class Col
    {
        public const string Accent = "#0067C0";      // WinUI accent
        public const string Text = "#1B1B1B";        // primary text
        public const string SubText = "#707070";     // secondary text
        public const string Panel = "#F6F6F6";       // panel background
        public const string PanelDark = "#EDEDED";   // inset control background
        public const string Border = "#DDDDDD";      // hairline

        public static Windows.UI.Color Hex(string hex)
        {
            hex = hex.TrimStart('#');
            return Windows.UI.Color.FromArgb(
                hex.Length == 8 ? Convert.ToByte(hex.Substring(0, 2), 16) : (byte)255,
                Convert.ToByte(hex.Substring(hex.Length - 6, 2), 16),
                Convert.ToByte(hex.Substring(hex.Length - 4, 2), 16),
                Convert.ToByte(hex.Substring(hex.Length - 2, 2), 16));
        }
        public static SolidColorBrush Brush(string hex) => new(Hex(hex));
        public static SolidColorBrush Brush(Windows.UI.Color c) => new(c);
        public static Windows.UI.Color Argb(byte a, byte r, byte g, byte b) => Windows.UI.Color.FromArgb(a, r, g, b);
    }

    public partial class EditorPage
    {
        private readonly Dictionary<EditorTool, Button> _railButtons = new();
        private BrushSettings? _savedBrushBeforeEraser;

        // ---------------- floating tool panel (docked left) ----------------

        private void OnToolFloatCollapseClick(object sender, RoutedEventArgs e)
        {
            ToolFloatPanel.Visibility = Visibility.Collapsed;
            ToolFloatPill.Visibility = Visibility.Visible;
        }

        private void OnToolFloatPillClick(object sender, RoutedEventArgs e)
        {
            ToolFloatPill.Visibility = Visibility.Collapsed;
            ToolFloatPanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// While a stroke is in progress the panel goes ghost and click-through so the
        /// canvas stays drawable underneath it.
        /// </summary>
        private void SetToolFloatGhost(bool ghost)
        {
            ToolFloatPanel.Opacity = ghost ? 0.15 : 1.0;
            ToolFloatPanel.IsHitTestVisible = !ghost;
            ToolFloatPill.Opacity = ghost ? 0.15 : 1.0;
            ToolFloatPill.IsHitTestVisible = !ghost;
        }

        // ---------------- left tool rail ----------------

        private void BuildToolRail()
        {
            ToolRail.Children.Clear();
            var defs = new (EditorTool tool, string glyph, string label)[]
            {
                (EditorTool.Transform, "\uE7C1", "Transform"),
                (EditorTool.MagicWand, "\uE7B8", "Magic Wand"),
                (EditorTool.Lasso, "\uE76C", "Lasso"),
                (EditorTool.FxFilter, "\uE8B3", "FX Filter"),
                (EditorTool.Brush, "\uE70F", "Brush"),
                (EditorTool.Eraser, "\uE75C", "Eraser"),
                (EditorTool.Smudge, "\uE8E9", "Smudge"),
                (EditorTool.Blur, "\uE719", "Blur"),
                (EditorTool.SpecialPen, "\uE735", "Special Pen"),
                (EditorTool.Bucket, "\uE7EF", "Bucket"),
            };
            foreach (var (tool, glyph, label) in defs)
            {
                var btn = new Button
                {
                    Style = (Style)Application.Current.Resources["ToolRailButton"],
                    Tag = tool
                };
                var sp = new StackPanel { Spacing = 1, HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new FontIcon { Glyph = glyph, FontSize = 20, Foreground = Col.Brush(Col.Text) });
                var tb = new TextBlock
                {
                    Text = label,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    MaxWidth = 58,
                    Foreground = Col.Brush(Col.Text),
                    LineHeight = 15
                };
                sp.Children.Add(tb);
                btn.Content = sp;
                btn.Click += (s, e) => SelectTool(tool);
                _railButtons[tool] = btn;
                ToolRail.Children.Add(btn);
            }
            SelectTool(EditorTool.Brush, silent: true);
        }

        private void SelectTool(EditorTool tool, bool silent = false)
        {
            _tool = tool;
            foreach (var (t, btn) in _railButtons)
            {
                bool active = t == tool;
                btn.Background = active ? Col.Brush("#CBE4F7") : null;
            }

            if (tool == EditorTool.Eraser)
            {
                _savedBrushBeforeEraser ??= _brush;
                _brush = new BrushSettings
                {
                    Name = "Eraser (Hard)",
                    Size = _brush.Size,
                    Opacity = 1.0,
                    Softness = 0.02,
                    Spacing = 0.05,
                    IsEraser = true
                };
            }
            else if (tool == EditorTool.Brush)
            {
                if (_savedBrushBeforeEraser != null && _brush.IsEraser)
                {
                    double size = _brush.Size;
                    _brush = _savedBrushBeforeEraser;
                    _brush.Size = size;
                    _savedBrushBeforeEraser = null;
                }
            }
            else if (tool == EditorTool.Bucket)
            {
                // bucket uses click handler
            }
            else if (tool == EditorTool.Transform)
            {
                TransformFrame.Visibility = Visibility.Visible;
            }
            else
            {
                TransformFrame.Visibility = Visibility.Collapsed;
            }

            if (!silent)
            {
                BuildToolPanelContent();
                if (tool is EditorTool.Brush or EditorTool.Eraser or EditorTool.SpecialPen or EditorTool.Smudge or EditorTool.Blur)
                    ShowToolPanel();
                else if (tool == EditorTool.FxFilter)
                    ShowFilterPanel();
                else
                    ToolPanelPopup.IsOpen = false;
            }
        }

        // ---------------- tool parameter panel (brush-like) ----------------

        private void BuildToolPanelContent()
        {
            var host = ToolPanelHost;
            host.Child = null;

            var grid = new Grid { Width = 980, Height = 470 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ---- left: category strip + tabs + brush list ----
            var left = new Grid { Margin = new Thickness(8, 8, 4, 8) };
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            left.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            left.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 0, 6) };
            tabs.SetValue(Grid.ColumnSpanProperty, 2);
            var basicBtn = TabButton("Basic (26)", active: true);
            var customBtn = TabButton("Custom");
            var onlineBtn = TabButton("Online");
            tabs.Children.Add(basicBtn);
            tabs.Children.Add(customBtn);
            tabs.Children.Add(onlineBtn);
            left.Children.Add(tabs);

            var catList = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Stretch };
            catList.SetValue(Grid.RowProperty, 1);
            var brushList = new ListBox
            {
                Background = Col.Brush("#FFFFFF"),
                Margin = new Thickness(4, 0, 0, 0),
                SelectionMode = SelectionMode.Single
            };
            brushList.SetValue(Grid.RowProperty, 1);
            brushList.SetValue(Grid.ColumnProperty, 1);

            var categories = new[] { "All", "Simple", "Sketch", "Comic", "Ink", "Airbrush", "Eraser" };
            var catButtons = new List<Button>();
            foreach (var cat in categories)
            {
                var b = new Button
                {
                    Style = (Style)Application.Current.Resources["SettingsTabButton"],
                    Content = new StackPanel
                    {
                        Spacing = 1,
                        Children =
                        {
                            new FontIcon { Glyph = CatGlyph(cat), FontSize = 18, Foreground = Col.Brush(Col.Text) },
                            new TextBlock { Text = cat, FontSize = 14, Foreground = Col.Brush(Col.Text), HorizontalAlignment = HorizontalAlignment.Center }
                        }
                    }
                };
                b.Click += (s, e) =>
                {
                    foreach (var cb in catButtons) cb.Background = null;
                    b.Background = Col.Brush("#CBE4F7");
                    FillBrushList(brushList, BrushCatalog.ByCategory(cat));
                };
                catButtons.Add(b);
                catList.Children.Add(b);
            }
            catButtons[0].Background = Col.Brush("#CBE4F7");
            left.Children.Add(catList);
            left.Children.Add(brushList);
            grid.Children.Add(left);
            FillBrushList(brushList, BrushCatalog.Basic);

            // ---- right: preview + params ----
            var right = new StackPanel { Margin = new Thickness(6, 8, 8, 8), Spacing = 8 };
            right.SetValue(Grid.ColumnProperty, 1);

            // preview card
            var previewCard = new Border
            {
                Background = Col.Brush(Col.Panel),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Height = 150
            };
            var previewStack = new StackPanel();
            var previewName = new TextBlock
            {
                Text = _brush.Name,
                Foreground = Col.Brush(Col.Text),
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            };
            var previewArea = new Border
            {
                Height = 90,
                Background = Col.Brush("#F0F0F0"),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 8, 0, 0),
                Child = new TextBlock
                {
                    Text = "Draw a stroke preview by dragging inside this area.",
                    Foreground = Col.Brush(Col.SubText),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                }
            };
            previewStack.Children.Add(previewName);
            previewStack.Children.Add(previewArea);
            previewCard.Child = previewStack;
            right.Children.Add(previewCard);

            // settings tab strip
            var settingTabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Background = Col.Brush(Col.Panel) };
            string[] stabs = { "Basic", "Fade", "Shape", "Filter", "Type", "Dynamic", "Settings" };
            foreach (var t in stabs)
            {
                var b = TabButton(t, t == "Basic");
                settingTabs.Children.Add(b);
            }
            right.Children.Add(settingTabs);

            // sliders
            var paramsHost = new StackPanel { Spacing = 6 };
            right.Children.Add(paramsHost);

            paramsHost.Children.Add(ParamSlider("Start Thickness", 0, 100, _brush.StartThickness, "%",
                v => { _brush.StartThickness = v; }));
            paramsHost.Children.Add(ParamSlider("End Thickness", 0, 100, _brush.EndThickness, "%",
                v => { _brush.EndThickness = v; }));
            paramsHost.Children.Add(ParamSlider("Thickness", 1, 100, _brush.Size, "px",
                v => { _brush.Size = v; SizeSlider.Value = v; UpdateSizePreview(); }));
            paramsHost.Children.Add(ParamSlider("Opacity", 0, 100, _brush.Opacity * 100, "%",
                v => { _brush.Opacity = v / 100.0; }));

            grid.Children.Add(right);
            host.Child = grid;

            static string CatGlyph(string cat) => cat switch
            {
                "All" => "\uE71D",
                "Simple" => "\uE70F",
                "Sketch" => "\uE735",
                "Comic" => "\uE8A9",
                "Ink" => "\uE765",
                "Airbrush" => "\uE78D",
                "Eraser" => "\uE75C",
                _ => "\uE70F"
            };
        }

        private void FillBrushList(ListBox list, List<BrushCatalog.Entry> entries)
        {
            list.Items.Clear();
            foreach (var e in entries)
            {
                var row = new Grid { Padding = new Thickness(8, 4, 8, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameCol = new StackPanel { Spacing = 2 };
                nameCol.Children.Add(new TextBlock
                {
                    Text = e.Name,
                    Foreground = Col.Brush(Col.Text),
                    FontSize = 14
                });
                // stroke preview line
                var line = new Border
                {
                    Height = 14,
                    Background = Col.Brush("#F0F0F0"),
                    CornerRadius = new CornerRadius(2),
                    Child = new TextBlock { Text = "", FontSize = 14 }
                };
                nameCol.Children.Add(line);
                row.Children.Add(nameCol);

                row.Children.Add(new TextBlock
                {
                    Text = e.Size.ToString("0.#"),
                    Foreground = Col.Brush(Col.SubText),
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 8, 0)
                });
                Grid.SetColumn((FrameworkElement)row.Children[1], 1);

                var add = new Button
                {
                    Content = new FontIcon { Glyph = "\uE948", FontSize = 14 },
                    Style = (Style)Application.Current.Resources["MiniRoundButton"]
                };
                ToolTipService.SetToolTip(add, "Add to Custom");
                row.Children.Add(add);
                Grid.SetColumn((FrameworkElement)row.Children[2], 2);

                var item = new ListBoxItem { Content = row, Tag = e };
                item.Tapped += (s, e2) =>
                {
                    ApplyCatalogEntry(e);
                    if (ToolPanelHost.Child is Grid g && g.Children.Count > 1 && g.Children[1] is StackPanel rp)
                    {
                        // update preview name
                        if (rp.Children[0] is Border b && b.Child is StackPanel sp && sp.Children[0] is TextBlock tb)
                            tb.Text = e.Name;
                    }
                };
                list.Items.Add(item);
            }
        }

        private void ApplyCatalogEntry(BrushCatalog.Entry e)
        {
            double size = _brush.Size;
            _brush = new BrushSettings
            {
                Name = e.Name,
                Size = size,
                Opacity = e.Opacity,
                Softness = e.Softness,
                Spacing = e.Spacing,
                IsEraser = e.Kind == "Eraser" || _tool == EditorTool.Eraser
            };
            SizeSlider.Value = size;
            UpdateSizePreview();
        }

        private Button TabButton(string label, bool active = false)
        {
            return new Button
            {
                Content = new TextBlock
                {
                    Text = label,
                    FontSize = 14,
                    Foreground = active ? Col.Brush("#005FB8") : Col.Brush(Col.Text)
                },
                Background = active ? Col.Brush("#CBE4F7") : Col.Brush(Col.Panel),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 7, 14, 7),
                MinHeight = 40
            };
        }

        private FrameworkElement ParamSlider(string label, double min, double max, double value, string unit, Action<double> setter)
        {
            var grid = new Grid { Margin = new Thickness(2, 0, 2, 0) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = label, Foreground = Col.Brush(Col.Text), FontSize = 14 });
            var val = new TextBlock { Text = $"{value:0.#}{unit}", Foreground = Col.Brush(Col.SubText), FontSize = 14, Margin = new Thickness(0, 0, 40, 0) };
            head.Children.Add(val);
            Grid.SetColumn(val, 1);

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var minus = new Button { Style = (Style)Application.Current.Resources["MiniRoundButton"], Content = new FontIcon { Glyph = "\uE949", FontSize = 14 } };
            var plus = new Button { Style = (Style)Application.Current.Resources["MiniRoundButton"], Content = new FontIcon { Glyph = "\uE948", FontSize = 14 } };
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
            minus.Click += (s, e) => slider.Value = Math.Max(min, slider.Value - Math.Max(0.1, (max - min) / 100));
            plus.Click += (s, e) => slider.Value = Math.Min(max, slider.Value + Math.Max(0.1, (max - min) / 100));
            slider.ValueChanged += (s, e) =>
            {
                val.Text = $"{e.NewValue:0.#}{unit}";
                setter(e.NewValue);
            };
            row.Children.Add(minus);
            row.Children.Add(slider);
            Grid.SetColumn(slider, 1);
            row.Children.Add(plus);
            Grid.SetColumn(plus, 2);

            grid.Children.Add(head);
            grid.Children.Add(row);
            Grid.SetRow(row, 1);
            return grid;
        }

        // ---------------- FX filter panel ----------------

        private void ShowFilterPanel()
        {
            var host = ToolPanelHost;
            var panel = new StackPanel { Margin = new Thickness(10), Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = "FX Filter",
                Foreground = Col.Brush(Col.Text),
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            });
            var grid = new GridView
            {
                Width = 960,
                Height = 380,
                Background = Col.Brush("#FFFFFF")
            };
            foreach (var f in FilterCatalog.Filters)
            {
                var cell = new StackPanel
                {
                    Width = 180,
                    Margin = new Thickness(6),
                    Spacing = 6
                };
                cell.Children.Add(new Border
                {
                    Height = 80,
                    Background = Col.Brush(Col.PanelDark),
                    CornerRadius = new CornerRadius(4),
                    Child = new FontIcon { Glyph = "\uE8B3", FontSize = 26, Foreground = Col.Brush("#9A9A9A") }
                });
                cell.Children.Add(new TextBlock
                {
                    Text = f,
                    Foreground = Col.Brush(Col.Text),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                });
                var item = new GridViewItem { Content = cell, Tag = f };
                item.Tapped += (s, e) => ApplyFilter(f);
                grid.Items.Add(item);
            }
            panel.Children.Add(grid);
            panel.Children.Add(new TextBlock
            {
                Text = "Tap a filter to apply it to the current layer. All filters are unlocked.",
                Foreground = Col.Brush(Col.SubText),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            });
            host.Child = panel;
            ShowToolPanel();
        }

        // ---------------- helpers ----------------

        private void ShowToolPanel()
        {
            ToolPanelPopup.IsOpen = true;
            var host = ToolPanelHost;
            host.UpdateLayout();
            var root = (FrameworkElement)Content;
            var pt = TransformVisualPoint(CanvasHost, root);
            // open just right of the floating tool panel (canvas starts at x=0 now)
            host.Margin = new Thickness(pt.X + 80, 6, 0, 0);
            host.VerticalAlignment = VerticalAlignment.Top;
            host.HorizontalAlignment = HorizontalAlignment.Left;
        }

        private Point TransformVisualPoint(UIElement from, UIElement to)
            => from.TransformToVisual(to).TransformPoint(new Point(0, 0));
    }
}
