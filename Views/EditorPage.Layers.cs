using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Pickers;
using Windows.Foundation;
using iPaint.Controls;
using iPaint.Models;
using iPaint.Services;
using WinUI = Microsoft.UI.Xaml;

namespace iPaint.Views
{
    public partial class EditorPage
    {
        // ================= layer panel =================

        private void BuildLayerPanel()
        {
            var host = LayerPanelHost;
            host.Child = null;
            var grid = new Grid { Padding = new Thickness(8, 8, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = "Layer",
                Foreground = Col.Brush(Col.Text),
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Margin = new Thickness(4, 0, 0, 8)
            });

            // canvas preview (left column)
            var canvasPreview = new Border
            {
                Background = Col.Brush(Col.Text),
                CornerRadius = new CornerRadius(2),
                Child = new Image { Source = _bitmap, Stretch = Stretch.Uniform, Margin = new Thickness(2) }
            };
            canvasPreview.SetValue(Grid.RowProperty, 1);
            grid.Children.Add(canvasPreview);

            // add/duplicate/import buttons under preview
            var underPreview = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
            underPreview.SetValue(Grid.RowProperty, 2);
            underPreview.Children.Add(MiniToolButton("\uE948", "Add Layer", () => AddLayer()));
            underPreview.Children.Add(MiniToolButton("\uE8C9", "Duplicate Layer", () => DuplicateLayer()));
            underPreview.Children.Add(MiniToolButton("\uE722", "Import Picture as Layer", ImportPictureToLayerAsync));
            grid.Children.Add(underPreview);

            // layer list (middle column)
            var list = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Stretch };
            list.SetValue(Grid.RowProperty, 1);
            list.SetValue(Grid.ColumnProperty, 1);
            grid.Children.Add(list);

            // Selection Layer row
            var selRow = new Grid { Background = Col.Brush("#F2F2F2"), Padding = new Thickness(8, 4, 8, 4) };
            selRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            selRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            selRow.Children.Add(new TextBlock
            {
                Text = "Selection Layer",
                Foreground = Col.Brush(Col.Text),
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            selRow.Children.Add(new TextBlock
            {
                Text = _selectionMask != null ? "Selected" : "No Selection",
                Foreground = Col.Brush(Col.Text),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            });
            list.Children.Add(selRow);

            // layers (top → bottom)
            for (int i = _doc.Layers.Count - 1; i >= 0; i--)
            {
                int idx = i;
                var layer = _doc.Layers[i];
                var row = new Grid
                {
                    Background = i == _activeLayerIndex ? Col.Brush("#CFE4F7") : Col.Brush("#EFEFEF"),
                    BorderBrush = Col.Brush("#8A8A8A"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6, 4, 6, 4),
                    RowSpacing = 0
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var thumb = new Border
                {
                    Width = 66,
                    Height = 66,
                    Background = BuildCheckerBrush(),
                    Child = new TextBlock
                    {
                        Text = layer.Name,
                        FontSize = 14,
                        Foreground = Col.Brush("#444444"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                row.Children.Add(thumb);

                var mid = new Grid { Margin = new Thickness(8, 0, 4, 0) };
                mid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mid.Children.Add(new TextBlock
                {
                    Text = layer.IsBackground ? "Background" : layer.Name,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
                var eye = new ToggleButton
                {
                    Content = new FontIcon { Glyph = layer.IsVisible ? "\uED1E" : "\uE8D5", FontSize = 16 },
                    Background = null,
                    BorderThickness = new Thickness(0),
                    IsChecked = layer.IsVisible // set before wiring handlers (crash-fix Rule 1)
                };
                eye.Checked += (s, e) => { layer.IsVisible = true; RefreshLayerRowIcons(); };
                eye.Unchecked += (s, e) => { layer.IsVisible = false; RefreshLayerRowIcons(); };
                eye.Click += (s, e) => FullComposite();
                mid.Children.Add(eye);
                Grid.SetRow(eye, 1);
                row.Children.Add(mid);
                Grid.SetColumn(mid, 1);

                var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 0 };
                right.Children.Add(new TextBlock
                {
                    Text = $"{layer.Opacity * 100:0}%",
                    FontSize = 14,
                    Foreground = Col.Brush(Col.Text),
                    HorizontalAlignment = HorizontalAlignment.Right
                });
                right.Children.Add(new TextBlock
                {
                    Text = layer.BlendMode,
                    FontSize = 14,
                    Foreground = Col.Brush(Col.Text),
                    HorizontalAlignment = HorizontalAlignment.Right
                });
                row.Children.Add(right);
                Grid.SetColumn(right, 2);

                row.Tapped += (s, e) =>
                {
                    _activeLayerIndex = idx;
                    BuildLayerPanel();
                };
                list.Children.Add(row);
            }

            // background row
            var bgRow = new Grid { Background = Col.Brush("#EFEFEF"), BorderBrush = Col.Brush("#8A8A8A"), BorderThickness = new Thickness(1), Padding = new Thickness(8, 4, 8, 4) };
            bgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bgRow.Children.Add(new TextBlock { Text = "Background", FontSize = 16, Foreground = Col.Brush(Col.Text), VerticalAlignment = VerticalAlignment.Center });
            var bgChoices = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var (label, mode) in new[] { ("White", "White"), ("Transparent", "Transparent"), ("Dark", "Dark") })
            {
                var b = new ToggleButton
                {
                    Width = 30,
                    Height = 30,
                    Padding = new Thickness(2),
                    Background = mode switch
                    {
                        "White" => Col.Brush(Col.Text),
                        "Dark" => Col.Brush("#222222"),
                        _ => BuildCheckerBrush()
                    },
                    BorderThickness = new Thickness(2),
                    BorderBrush = _doc.BackgroundType == mode ? Col.Brush("#2196F3") : Col.Brush("#888888"),
                    Tag = mode
                };
                b.Click += (s, e) =>
                {
                    PushUndoSnapshot();
                    _doc.BackgroundType = mode;
                    FullComposite();
                };
                bgChoices.Children.Add(b);
            }
            bgRow.Children.Add(bgChoices);
            Grid.SetColumn(bgChoices, 1);
            list.Children.Add(bgRow);

            // blend + opacity row (below list)
            var below = new Grid { Margin = new Thickness(8, 6, 8, 0) };
            below.SetValue(Grid.RowProperty, 2);
            below.SetValue(Grid.ColumnProperty, 1);
            below.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            below.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            below.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            below.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            below.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var mergeBtn = MiniToolButton("\uE74A", "Merge Down", MergeDown);
            mergeBtn.Margin = new Thickness(0, 0, 10, 0);
            below.Children.Add(mergeBtn);
            var alphaBtn = MiniToggle("\uE72E", "Alpha Lock", ActiveLayer.AlphaLock, v => ActiveLayer.AlphaLock = v);
            below.Children.Add(alphaBtn);
            Grid.SetColumn(alphaBtn, 1);

            var combo = new ComboBox
            {
                MinWidth = 160,
                ItemsSource = new[] { "Normal", "Multiply", "Screen", "Add", "Overlay" },
                SelectedItem = ActiveLayer.BlendMode,
                Margin = new Thickness(10, 0, 0, 0)
            };
            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedItem is string m)
                {
                    PushUndoSnapshot();
                    ActiveLayer.BlendMode = m;
                    FullComposite();
                }
            };
            below.Children.Add(combo);
            Grid.SetColumn(combo, 2);

            var opRow = ParamSlider("Layer Opacity", 0, 100, ActiveLayer.Opacity * 100, "%", v =>
            {
                ActiveLayer.Opacity = v / 100.0;
                FullComposite();
            });
            opRow.SetValue(Grid.RowProperty, 1);
            opRow.SetValue(Grid.ColumnSpanProperty, 3);
            below.Children.Add(opRow);
            grid.Children.Add(below);

            // right command rail
            var rail = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Stretch, Background = Col.Brush(Col.Panel), Padding = new Thickness(0, 6, 0, 6) };
            rail.SetValue(Grid.RowSpanProperty, 3);
            rail.SetValue(Grid.ColumnProperty, 2);
            rail.Children.Add(MiniToolButton("\uE8B7", "Clear Layer", () => { PushUndoSnapshot(); BrushEngine.ClearLayer(ActiveLayer); FullComposite(); }));
            rail.Children.Add(MiniToolButton("\uE74D", "Delete Layer", DeleteLayer));
            rail.Children.Add(MiniToolButton("\uE8AB", "Clip to Layer Below", () => { ActiveLayer.IsClipping = !ActiveLayer.IsClipping; FullComposite(); BuildLayerPanel(); }));
            rail.Children.Add(MiniToolButton("\uE8C8", "Move Layer Up", () => MoveLayer(1)));
            rail.Children.Add(MiniToolButton("\uE8CB", "Move Layer Down", () => MoveLayer(-1)));
            rail.Children.Add(MiniToolButton("\uE8E9", "Flip Horizontal", () => { PushUndoSnapshot(); BrushEngine.FlipLayer(ActiveLayer, true); FullComposite(); }));
            rail.Children.Add(MiniToolButton("\uE8CA", "Flip Vertical", () => { PushUndoSnapshot(); BrushEngine.FlipLayer(ActiveLayer, false); FullComposite(); }));
            rail.Children.Add(MiniToolButton("\uE8D4", "Invert Colors", () => { PushUndoSnapshot(); InvertLayerColors(ActiveLayer); FullComposite(); }));
            grid.Children.Add(rail);

            host.Child = grid;
        }

        private void RefreshLayerRowIcons() => FullComposite();

        private Brush BuildCheckerBrush()
        {
            var wb = new WriteableBitmap(16, 16);
            var buf = wb.PixelBuffer;
            var bytes = buf.ToArray();
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    int o = (y * 16 + x) * 4;
                    bool light = ((x / 8) + (y / 8)) % 2 == 0;
                    bytes[o] = bytes[o + 1] = bytes[o + 2] = light ? (byte)0xCC : (byte)0x99;
                    bytes[o + 3] = 255;
                }
            buf.AsStream().Write(bytes, 0, bytes.Length);
            wb.Invalidate();
            return new ImageBrush { ImageSource = wb, Stretch = Stretch.None };
        }

        private Button MiniToolButton(string glyph, string tooltip, Action action)
        {
            var b = new Button
            {
                Content = new FontIcon { Glyph = glyph, FontSize = 18, Foreground = Col.Brush(Col.Text) },
                Background = Col.Brush(Col.PanelDark),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8),
                MinHeight = 40
            };
            ToolTipService.SetToolTip(b, tooltip);
            b.Click += (s, e) => action();
            return b;
        }

        private FrameworkElement MiniToggle(string glyph, string tooltip, bool value, Action<bool> setter)
        {
            var tb = new ToggleButton
            {
                Content = new FontIcon { Glyph = glyph, FontSize = 18, Foreground = Col.Brush(Col.Text) },
                Background = value ? Col.Brush("#2196F3") : Col.Brush(Col.PanelDark),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8),
                MinHeight = 40,
                IsChecked = value
            };
            ToolTipService.SetToolTip(tb, tooltip);
            tb.Click += (s, e) => { setter(tb.IsChecked == true); FullComposite(); };
            return tb;
        }

        private void AddLayer()
        {
            PushUndoSnapshot();
            _doc.Layers.Add(new PaintLayer(_doc.Width, _doc.Height, false)
            {
                Name = (_doc.Layers.Count(l => !l.IsBackground) + 1).ToString()
            });
            _activeLayerIndex = _doc.Layers.Count - 1;
            LayerCountText.Text = _doc.Layers.Count(l => !l.IsBackground).ToString();
            BuildLayerPanel();
        }

        private void DuplicateLayer()
        {
            PushUndoSnapshot();
            var clone = ActiveLayer.Clone();
            clone.Name = ActiveLayer.IsBackground ? "Background copy" : ActiveLayer.Name + " copy";
            _doc.Layers.Insert(_activeLayerIndex + 1, clone);
            _activeLayerIndex++;
            LayerCountText.Text = _doc.Layers.Count(l => !l.IsBackground).ToString();
            BuildLayerPanel();
            FullComposite();
        }

        private void DeleteLayer()
        {
            if (_doc.Layers.Count <= 1) return;
            PushUndoSnapshot();
            _doc.Layers.RemoveAt(_activeLayerIndex);
            if (_activeLayerIndex >= _doc.Layers.Count) _activeLayerIndex = _doc.Layers.Count - 1;
            LayerCountText.Text = Math.Max(1, _doc.Layers.Count(l => !l.IsBackground)).ToString();
            BuildLayerPanel();
            FullComposite();
        }

        private void MoveLayer(int dir)
        {
            int target = _activeLayerIndex + dir;
            if (target < 0 || target >= _doc.Layers.Count) return;
            PushUndoSnapshot();
            (_doc.Layers[_activeLayerIndex], _doc.Layers[target]) = (_doc.Layers[target], _doc.Layers[_activeLayerIndex]);
            _activeLayerIndex = target;
            BuildLayerPanel();
            FullComposite();
        }

        private void MergeDown()
        {
            if (_activeLayerIndex == 0) return;
            PushUndoSnapshot();
            var top = ActiveLayer;
            var bottom = _doc.Layers[_activeLayerIndex - 1];
            double op = top.Opacity;
            for (int i = 0; i < top.BgraPixels.Length; i += 4)
            {
                double sa = top.BgraPixels[i + 3] / 255.0 * op;
                if (sa <= 0) continue;
                double da = bottom.BgraPixels[i + 3] / 255.0;
                double outA = sa + da * (1 - sa);
                if (outA <= 0) { bottom.BgraPixels[i + 3] = 0; continue; }
                for (int c = 0; c < 3; c++)
                {
                    int o = i + c;
                    bottom.BgraPixels[o] = (byte)Math.Clamp(
                        (top.BgraPixels[o] * sa + bottom.BgraPixels[o] * da * (1 - sa)) / outA, 0, 255);
                }
                bottom.BgraPixels[i + 3] = (byte)Math.Clamp(outA * 255, 0, 255);
            }
            _doc.Layers.RemoveAt(_activeLayerIndex);
            _activeLayerIndex--;
            BuildLayerPanel();
            FullComposite();
        }

        private static void InvertLayerColors(PaintLayer layer)
        {
            var px = layer.BgraPixels;
            for (int i = 0; i < px.Length; i += 4)
            {
                if (px[i + 3] == 0) continue;
                px[i] = (byte)(255 - px[i]);
                px[i + 1] = (byte)(255 - px[i + 1]);
                px[i + 2] = (byte)(255 - px[i + 2]);
            }
        }

        private async void ImportPictureToLayerAsync()
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            var (pixels, w, h) = await ArtworkStore.DecodeImageAsync(file.Path);
            PushUndoSnapshot();
            var layer = new PaintLayer(_doc.Width, _doc.Height, false) { Name = "Picture" };
            // center-crop / scale 1:1 into canvas
            int ox = Math.Max(0, (_doc.Width - w) / 2);
            int oy = Math.Max(0, (_doc.Height - h) / 2);
            int cw = Math.Min(w, _doc.Width - ox), ch = Math.Min(h, _doc.Height - oy);
            for (int y = 0; y < ch; y++)
            {
                Buffer.BlockCopy(pixels, y * w * 4, layer.BgraPixels, ((oy + y) * _doc.Width + ox) * 4, cw * 4);
            }
            _doc.Layers.Add(layer);
            _activeLayerIndex = _doc.Layers.Count - 1;
            LayerCountText.Text = _doc.Layers.Count(l => !l.IsBackground).ToString();
            BuildLayerPanel();
            FullComposite();
        }

        // ================= color panel =================

        private IbisColorWheel? _wheel;

        private void BuildColorPanel()
        {
            var host = ColorPanelHost;
            host.Child = null;

            var grid = new Grid { RowSpacing = 6, Margin = new Thickness(10, 8, 10, 8) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new Grid();
            var colorTitle = new TextBlock
            {
                Text = "Color",
                Foreground = Col.Brush(Col.Text),
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            };
            head.Children.Add(colorTitle);
            var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, HorizontalAlignment = HorizontalAlignment.Right };
            swatches.Children.Add(new Border { Width = 34, Height = 34, Background = Col.Brush(_prevColor), BorderBrush = Col.Brush("#222222"), BorderThickness = new Thickness(1) });
            swatches.Children.Add(new Border { Width = 34, Height = 34, Background = Col.Brush(_color), BorderBrush = Col.Brush("#222222"), BorderThickness = new Thickness(1) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(swatches);
            Grid.SetColumn(swatches, 1);
            grid.Children.Add(head);

            // Wheel
            var wheelHost = new Border { Height = 300, Child = null };
            _wheel = new IbisColorWheel { Width = 280, Height = 280, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _wheel.SetColor(0, 0, 0);
            _wheel.ColorChanged += () =>
            {
                _color = IbisColorWheel.HsbToColor(_wheel.Hue, _wheel.Saturation, _wheel.Brightness);
                UpdateColorUI();
            };
            wheelHost.Child = _wheel;
            wheelHost.SetValue(Grid.RowProperty, 1);
            grid.Children.Add(wheelHost);

            // palette grid (standard colors)
            var palette = new WrapGraph();
            string[] paletteColors =
            {
                "#000000", "#FFFFFF", "#FF0000", "#FF7F00", "#FFBF00", "#FFFF00", "#BFFF00", "#00FF00",
                "#00FF40", "#00FF80", "#00FFBF", "#00FFFF", "#00BFFF", "#007FFF", "#0040FF", "#4000FF",
                "#7F00FF", "#BF00FF", "#FF00FF", "#FF00BF", "#FF0080", "#FFB6C1", "#F5DEB3", "#FFDBAC",
                "#E0AC69", "#C68642", "#8D5524", "#FFF5E1", "#FFE4C4", "#E8D5B7", "#D2B48C", "#A0785A"
            };
            foreach (var hexColor in paletteColors)
            {
                var swatch = new Button
                {
                    Width = 36,
                    Height = 36,
                    Background = Col.Brush(hexColor),
                    BorderBrush = Col.Brush(Col.PanelDark),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(0)
                };
                var captured = hexColor;
                swatch.Click += (s, e) =>
                {
                    _color = Col.Hex(captured);
                    if (_wheel != null)
                    {
                        var (h2, s2, v2) = IbisColorWheel.ColorToHsb(_color);
                        _wheel.SetColor(h2, s2, v2);
                    }
                    UpdateColorUI();
                };
                palette.Children.Add(swatch);
            }
            grid.Children.Add(palette);
            palette.SetValue(Grid.RowProperty, 2);

            // sliders
            var sliders = new StackPanel { Spacing = 2 };
            sliders.SetValue(Grid.RowProperty, 3);
            sliders.Children.Add(ParamSlider("Hue", 0, 360, 0, "\u00B0", v => { if (_wheel != null) { _wheel.SetColor(v, Sat(), Bri()); _color = IbisColorWheel.HsbToColor(v, Sat(), Bri()); UpdateColorUI(); } }));
            sliders.Children.Add(ParamSlider("Saturation", 0, 100, 0, "%", v => { if (_wheel != null) { _wheel.SetColor(_wheel.Hue, v / 100.0, Bri()); _color = IbisColorWheel.HsbToColor(_wheel.Hue, v / 100.0, Bri()); UpdateColorUI(); } }));
            sliders.Children.Add(ParamSlider("Brightness", 0, 100, 0, "%", v => { if (_wheel != null) { _wheel.SetColor(_wheel.Hue, Sat(), v / 100.0); _color = IbisColorWheel.HsbToColor(_wheel.Hue, Sat(), v / 100.0); UpdateColorUI(); } }));
            grid.Children.Add(sliders);

            host.Child = grid;
        }

        private double Sat() => _wheel?.Saturation ?? 0;
        private double Bri() => _wheel?.Brightness ?? 0;

        private void UpdateColorUI()
        {
            CurrentColorRect.Fill = Col.Brush(_color);
            if (_wheel != null)
            {
                var (h, s, v) = IbisColorWheel.ColorToHsb(_color);
                HueSliderHostSetValue(h);
            }
        }

        private void HueSliderHostSetValue(double h) { /* sliders track via ParamSlider local state */ }

        // ================= top flyouts =================

        private void ShowTopFlyout(FrameworkElement content, FrameworkElement anchor)
        {
            TopFlyoutHost.Child = content;
            TopFlyoutPopup.IsOpen = true;
            var root = (FrameworkElement)Content;
            var pt = TransformVisualPoint(anchor, root);
            double w = double.IsNaN(content.Width) ? 340 : content.Width;
            TopFlyoutHost.Margin = new Thickness(Math.Max(8, pt.X - w + 50), 88, 0, 0);
            TopFlyoutHost.HorizontalAlignment = HorizontalAlignment.Left;
            TopFlyoutHost.VerticalAlignment = VerticalAlignment.Top;
        }

        private void BuildCanvasSettingsFlyout()
        {
            var s = MainWindow.Current.Settings;
            var panel = new StackPanel { Spacing = 8, Margin = new Thickness(12) };

            panel.Children.Add(FlyoutToggle("Reference Window", s.ReferenceWindow, v => { s.ReferenceWindow = v; MainWindow.Current.SaveSettings(); ReferenceWindow.Visibility = v ? Visibility.Visible : Visibility.Collapsed; }));
            panel.Children.Add(FlyoutToggle("Grid", s.Grid, v => { s.Grid = v; MainWindow.Current.SaveSettings(); ApplyGridOverlay(); }));
            panel.Children.Add(ParamSlider("Grid Divisions", 1, 12, s.GridDivisions, "", v => { s.GridDivisions = (int)v; MainWindow.Current.SaveSettings(); ApplyGridOverlay(); }));
            panel.Children.Add(FlyoutToggle("Manga Manuscript", s.MangaManuscript, v => { s.MangaManuscript = v; MainWindow.Current.SaveSettings(); ApplyGridOverlay(); }));

            var seg = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            seg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            seg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var smooth = new ToggleButton { Content = new TextBlock { Text = "Smooth", FontSize = 14 }, BorderThickness = new Thickness(0), Padding = new Thickness(10, 8, 10, 8) };
            var pixel = new ToggleButton { Content = new TextBlock { Text = "Pixelated", FontSize = 14 }, BorderThickness = new Thickness(0), Padding = new Thickness(10, 8, 10, 8) };
            smooth.Background = s.DisplayWhenZoomed == "Smooth" ? Col.Brush("#CBE4F7") : Col.Brush("#EDEDED");
            pixel.Background = s.DisplayWhenZoomed == "Pixelated" ? Col.Brush("#CBE4F7") : Col.Brush("#EDEDED");
            smooth.Click += (s2, e2) => { s.DisplayWhenZoomed = "Smooth"; MainWindow.Current.SaveSettings(); CanvasImage.CacheMode = null; smooth.Background = Col.Brush("#CBE4F7"); pixel.Background = Col.Brush("#EDEDED"); };
            pixel.Click += (s2, e2) => { s.DisplayWhenZoomed = "Pixelated"; MainWindow.Current.SaveSettings(); CanvasImage.CacheMode = new BitmapCache(); smooth.Background = Col.Brush("#EDEDED"); pixel.Background = Col.Brush("#CBE4F7"); };
            seg.Children.Add(smooth);
            seg.Children.Add(pixel);
            Grid.SetColumn(pixel, 1);
            panel.Children.Add(new TextBlock { Text = "Display When Zoomed", Foreground = Col.Brush(Col.Text), FontSize = 14 });
            panel.Children.Add(seg);
            ShowTopFlyout(panel, CanvasSettingsButton);
        }

        private FrameworkElement FlyoutToggle(string label, bool value, Action<bool> setter)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new TextBlock { Text = label, Foreground = Col.Brush(Col.Text), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            var t = new ToggleSwitch { IsOn = value, OnContent = "", OffContent = "", MinWidth = 90 };
            t.Toggled += (s, e) => setter(t.IsOn);
            grid.Children.Add(t);
            Grid.SetColumn(t, 1);
            return grid;
        }

        private void BuildSelectionFlyout()
        {
            var panel = new StackPanel { Margin = new Thickness(12), Spacing = 4 };
            void Row(string glyph, string label, Action action, bool enabled = true)
            {
                var btn = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    IsEnabled = enabled,
                    Background = null,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = Col.Brush("#EDEDED"),
                    Padding = new Thickness(8, 8, 8, 8)
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                sp.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, Foreground = enabled ? Col.Brush(Col.Text) : Col.Brush(Col.SubText) });
                sp.Children.Add(new TextBlock { Text = label, FontSize = 14, Foreground = enabled ? Col.Brush(Col.Text) : Col.Brush(Col.SubText), VerticalAlignment = VerticalAlignment.Center });
                btn.Content = sp;
                btn.Click += (s, e) => { TopFlyoutPopup.IsOpen = false; action(); };
                panel.Children.Add(btn);
            }
            Row("\uE891", "Remove Selection Area", ClearSelection, _selectionMask != null);
            Row("\uE892", "Invert Selection Area", InvertSelection, _selectionMask != null);
            Row("\uE74D", "Clear Layer", () => { PushUndoSnapshot(); BrushEngine.ClearLayer(ActiveLayer); FullComposite(); });
            Row("\uE7E6", "Select Opacity", SelectByOpacity);
            Row("\uE8E9", "Select Color Range", () => ShowInfo("Select Color Range", "Tap the canvas with the Magic Wand tool to select a color range."));
            Row("\uE8C8", "Cut", () => { PushUndoSnapshot(); CutSelectionToInternal(); });
            Row("\uE8C9", "Copy Selection", () => ShowInfo("Copy", "Selection copied to internal clipboard. Use Paste to add it as a new layer."));
            Row("\uE8E9", "Paste as New Layer", () => PasteInternalAsLayer());
            ShowTopFlyout(panel, SelectionButton);
        }

        private (byte[] px, int w, int h)? _internalClipboard;

        private void CutSelectionToInternal()
        {
            var layer = ActiveLayer;
            var px = layer.BgraPixels;
            var clip = new byte[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                bool selected = _selectionMask == null || _selectionMask[i / 4] != 0;
                clip[i] = selected ? px[i] : (byte)0;
                if (selected && !_selectionMask_equal_all()) px[i] = 0;
            }
            _internalClipboard = (clip, layer.Width, layer.Height);
            FullComposite();
        }

        private bool _selectionMask_equal_all()
        {
            // when there is no selection we do not erase the source
            return _selectionMask == null;
        }

        private void PasteInternalAsLayer()
        {
            if (_internalClipboard == null) { ShowInfo("Paste", "Clipboard is empty."); return; }
            PushUndoSnapshot();
            var layer = new PaintLayer(_doc.Width, _doc.Height, false) { Name = "Pasted" };
            Buffer.BlockCopy(_internalClipboard.Value.px, 0, layer.BgraPixels, 0,
                Math.Min(_internalClipboard.Value.px.Length, layer.BgraPixels.Length));
            _doc.Layers.Add(layer);
            _activeLayerIndex = _doc.Layers.Count - 1;
            LayerCountText.Text = _doc.Layers.Count(l => !l.IsBackground).ToString();
            BuildLayerPanel();
            FullComposite();
        }

        private void BuildStabilizerFlyout()
        {
            var s = MainWindow.Current.Settings;
            var panel = new StackPanel { Margin = new Thickness(12), Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = "Stabilizer", Foreground = Col.Brush(Col.Text), FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            panel.Children.Add(ParamSlider("Stabilizer", 0, 15, s.StabilizerValue, "", v => { s.StabilizerValue = v; MainWindow.Current.SaveSettings(); }));
            panel.Children.Add(new TextBlock { Text = "Method", Foreground = Col.Brush(Col.Text), FontSize = 14 });
            var seg = new Grid();
            seg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            seg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var rt = new ToggleButton { Content = new TextBlock { Text = "Real Time", FontSize = 14 }, Padding = new Thickness(10, 8, 10, 8), BorderThickness = new Thickness(0) };
            var af = new ToggleButton { Content = new TextBlock { Text = "After", FontSize = 14 }, Padding = new Thickness(10, 8, 10, 8), BorderThickness = new Thickness(0) };
            rt.Background = s.StabilizerMethod == "Real Time" ? Col.Brush("#CBE4F7") : Col.Brush("#EDEDED");
            af.Background = s.StabilizerMethod == "After" ? Col.Brush("#CBE4F7") : Col.Brush("#EDEDED");
            rt.Click += (s2, e2) => { s.StabilizerMethod = "Real Time"; MainWindow.Current.SaveSettings(); rt.Background = Col.Brush("#CBE4F7"); af.Background = Col.Brush("#EDEDED"); };
            af.Click += (s2, e2) => { s.StabilizerMethod = "After"; MainWindow.Current.SaveSettings(); rt.Background = Col.Brush("#EDEDED"); af.Background = Col.Brush("#CBE4F7"); };
            seg.Children.Add(rt);
            seg.Children.Add(af);
            panel.Children.Add(seg);
            ShowTopFlyout(panel, StabilizerButton);
        }

        private void BuildRulerFlyout()
        {
            var panel = new StackPanel { Margin = new Thickness(12), Spacing = 6 };
            panel.Children.Add(new TextBlock { Text = "Ruler", Foreground = Col.Brush(Col.Text), FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            var rulerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            foreach (var (label, mode) in new[] { ("Off", "Off"), ("Linear", "Linear") })
            {
                var b = new ToggleButton
                {
                    Content = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = label, FontSize = 14, Foreground = RulerMode == mode ? Col.Brush("#005FB8") : Col.Brush(Col.Text) }
                        }
                    },
                    Background = RulerMode == mode ? Col.Brush("#CBE4F7") : Col.Brush("#EDEDED"),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(10, 8, 10, 8),
                    Tag = mode
                };
                b.Click += (s, e) =>
                {
                    RulerMode = mode;
                    foreach (ToggleButton t in rulerRow.Children) t.Background = (string)t.Tag == mode ? Col.Brush("#CBE4F7") : Col.Brush("#EDEDED");
                };
                rulerRow.Children.Add(b);
            }
            panel.Children.Add(rulerRow);

            panel.Children.Add(new TextBlock { Text = "Symmetry Ruler", Foreground = Col.Brush(Col.Text), FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            var symRow = new WrapGraph();
            foreach (var (label, mode) in new[] { ("Off", "Off"), ("Vertical", "Vertical"), ("Horizontal", "Horizontal"), ("Quad", "Quad"), ("Radial 8", "Radial 8") })
            {
                var b = new ToggleButton
                {
                    Content = new TextBlock { Text = label, FontSize = 14, Foreground = SymmetryMode == mode ? Col.Brush("#005FB8") : Col.Brush(Col.Text) },
                    Background = SymmetryMode == mode ? Col.Brush("#CBE4F7") : Col.Brush("#EDEDED"),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(10, 8, 10, 8),
                    Tag = mode,
                    Margin = new Thickness(0, 0, 4, 4)
                };
                b.Click += (s, e) =>
                {
                    SymmetryMode = mode;
                    foreach (ToggleButton t in symRow.Children) t.Background = (string)t.Tag == mode ? Col.Brush("#CBE4F7") : Col.Brush("#EDEDED");
                };
                symRow.Children.Add(b);
            }
            panel.Children.Add(symRow);
            ShowTopFlyout(panel, RulerButton);
        }

        private void BuildMaterialsFlyout()
        {
            var panel = new Grid { Margin = new Thickness(12), RowSpacing = 6 };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(320) });

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = "Materials", Foreground = Col.Brush(Col.Text), FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            head.Children.Add(new TextBlock { Text = $"{FilterCatalog.Filters.Length * 762}", Foreground = Col.Brush(Col.SubText), FontSize = 14 });
            Grid.SetColumn((FrameworkElement)head.Children[1], 1);
            panel.Children.Add(head);

            var list = new ListBox { Background = Col.Brush(Col.PanelDark) };
            list.SetValue(Grid.RowProperty, 1);
            var groups = new[]
            {
                ("Tracing Sketches (Mini)", 56),
                ("Tracing Sketches (Whole Body)", 253),
                ("Tracing Sketches (Partial)", 342),
                ("Tracing Sketches (Building)", 48),
                ("Background Patterns", 63),
            };
            foreach (var (name, count) in groups)
            {
                var row = new Grid { Padding = new Thickness(8, 6, 8, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock { Text = name, Foreground = Col.Brush(Col.Text), FontSize = 14 });
                row.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = count.ToString(), Foreground = Col.Brush(Col.SubText), FontSize = 14 },
                        new FontIcon { Glyph = "\uE76C", FontSize = 14, Foreground = Col.Brush(Col.SubText) }
                    }
                });
                Grid.SetColumn((FrameworkElement)row.Children[1], 1);
                var item = new ListBoxItem { Content = row, Tag = name };
                item.Tapped += (s, e) => StampMaterialPattern(name.ToString());
                list.Items.Add(item);
            }
            panel.Children.Add(list);
            ShowTopFlyout(panel, MaterialsButton);
        }

        private void StampMaterialPattern(string group)
        {
            PushUndoSnapshot();
            var layer = ActiveLayer;
            var rnd = new Random(1234);
            switch (group)
            {
                case "Background Patterns":
                    for (int y = 0; y < layer.Height; y += 24)
                        for (int x = 0; x < layer.Width; x += 24)
                            BrushEngine.Stamp(layer, x, y, 6, Microsoft.UI.Colors.Gray, new BrushSettings { Opacity = 0.8, Softness = 0.1, Spacing = 1 });
                    break;
                default:
                    for (int i = 0; i < 260; i++)
                    {
                        double x = rnd.NextDouble() * layer.Width;
                        double y = rnd.NextDouble() * layer.Height;
                        BrushEngine.Stamp(layer, x, y, 2.2, Microsoft.UI.Colors.Gray, new BrushSettings { Opacity = 0.55, Softness = 0.4, Spacing = 1 });
                    }
                    break;
            }
            FullComposite();
            ShowInfo("Materials", $"'{group}' pattern stamped onto the current layer.");
        }

        private void ApplyGridOverlay()
        {
            var s = MainWindow.Current.Settings;
            GridOverlay.Children.Clear();
            if (s.Grid)
            {
                int div = Math.Max(1, s.GridDivisions);
                for (int i = 1; i < div; i++)
                {
                    double x = _doc.Width * i / (double)div;
                    double y = _doc.Height * i / (double)div;
                    GridOverlay.Children.Add(new Rectangle
                    {
                        Width = 1,
                        Height = _doc.Height,
                        Fill = Col.Brush("#6688CC88"),
                        Margin = new Thickness(x, 0, 0, 0)
                    });
                    GridOverlay.Children.Add(new Rectangle
                    {
                        Width = _doc.Width,
                        Height = 1,
                        Fill = Col.Brush("#6688CC88"),
                        Margin = new Thickness(0, y, 0, 0)
                    });
                }
            }
            // GridOverlay sits in canvas coordinates: match the canvas transform
            GridOverlay.Width = _doc.Width;
            GridOverlay.Height = _doc.Height;
        }

        private void ShowInfo(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            _ = dialog.ShowAsync();
        }
    }
}
