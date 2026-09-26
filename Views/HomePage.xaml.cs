using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using iPaint.Models;
using iPaint.Services;
using WinUI = Microsoft.UI.Xaml;

namespace iPaint.Views
{
    /// <summary>Start screen: Create New + artwork history, in WinUI 3 Fluent style.</summary>
    public sealed partial class HomePage : Page
    {
        private List<GalleryItem> _items = new();

        public HomePage()
        {
            InitializeComponent();
            LoadSamples();
            _ = ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            _items = ArtworkStore.LoadGallery();
            EmptyState.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryGrid.Items.Clear();

            foreach (var item in _items)
            {
                HistoryGrid.Items.Add(BuildHistoryCard(item));
            }
            await Task.CompletedTask;
        }

        private FrameworkElement BuildHistoryCard(GalleryItem item)
        {
            var root = new Grid { Width = 220 };

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var thumbBorder = new Border
            {
                Height = 200,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                BorderBrush = new SolidColorBrush(Col.Argb(30, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Child = new Border
                {
                    CornerRadius = new CornerRadius(7, 7, 0, 0),
                    Child = new Image
                    {
                        Source = System.IO.File.Exists(item.ThumbnailPath)
                            ? new BitmapImage(new Uri(item.ThumbnailPath))
                            : null,
                        Stretch = Stretch.Uniform
                    }
                }
            };

            var info = new StackPanel { Margin = new Thickness(4, 8, 4, 8), Spacing = 2 };
            info.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            info.Children.Add(new TextBlock
            {
                Text = $"{item.Width} × {item.Height}    Edited {item.LastModified:MM/dd HH:mm}",
                FontSize = 14,
                Foreground = new SolidColorBrush(Col.Argb(255, 0x70, 0x70, 0x70)),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            root.Children.Add(thumbBorder);
            root.Children.Add(info);
            Grid.SetRow(info, 1);

            var btn = new Button
            {
                Content = root,
                Background = null,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 12, 12),
                Tag = item
            };
            btn.Click += (s, e) => MainWindow.Current.OpenArtwork(item);

            // Context menu: Open / Delete
            var menu = new MenuFlyout();
            var open = new MenuFlyoutItem { Text = "Open" };
            open.Click += (s, e) => MainWindow.Current.OpenArtwork(item);
            var delete = new MenuFlyoutItem { Text = "Delete" };
            delete.Click += (s, e) => _ = DeleteItemAsync(item);
            menu.Items.Add(open);
            menu.Items.Add(delete);
            btn.ContextFlyout = menu;

            return btn;
        }

        private async Task DeleteItemAsync(GalleryItem item)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete artwork",
                Content = $"Delete '{item.Title}'? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;
            ArtworkStore.Delete(item);
            await ReloadAsync();
        }

        // ---------- Create New ----------

        private async void OnCreateNewClick(object sender, RoutedEventArgs e)
        {
            int w = 2000, h = 2000;

            var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
            panel.Children.Add(new TextBlock
            {
                Text = "Choose a canvas size:",
                FontSize = 14,
                Foreground = new SolidColorBrush(Col.Argb(255, 0x70, 0x70, 0x70))
            });

            var list = new ListView { Height = 264 };
            var presets = new (string label, int w, int h)[]
            {
                ("Custom (2000 × 2000)", 2000, 2000),
                ("HD (Quality)  2048 × 1536", 2048, 1536),
                ("1 : 1 (2K)  2048 × 2048", 2048, 2048),
                ("3 : 4 (2K)  2048 × 1536", 2048, 1536),
                ("9 : 16 (2K)  1920 × 1080", 1920, 1080),
                ("Full HD  1920 × 1080", 1920, 1080),
                ("Square (1K)  1024 × 1024", 1024, 1024),
            };
            foreach (var p in presets)
            {
                list.Items.Add(new ListViewItem
                {
                    Content = new TextBlock { Text = p.label, FontSize = 15 },
                    Tag = (p.w, p.h),
                    Padding = new Thickness(12, 8, 12, 8)
                });
            }
            list.SelectedIndex = 0;
            list.SelectionChanged += (s, e) =>
            {
                if (list.SelectedItem is ListViewItem li && li.Tag is ValueTuple<int, int> size)
                {
                    w = size.Item1;
                    h = size.Item2;
                }
            };
            panel.Children.Add(list);

            var dialog = new ContentDialog
            {
                Title = "New artwork",
                Content = panel,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                CreateDocument(w, h);
            }
        }

        private void CreateDocument(int w, int h)
        {
            w = Math.Min(w, 4096);
            h = Math.Min(h, 4096);
            var doc = new ArtworkDocument
            {
                Title = ArtworkDocument.NextUntitledName(),
                Width = w,
                Height = h,
                BackgroundType = "White"
            };
            doc.Layers.Add(new PaintLayer(w, h, false));
            MainWindow.Current.NavigateToEditor(doc);
        }

        // ---------- Import ----------

        private async void OnImportClick(object sender, RoutedEventArgs e)
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
            var doc = new ArtworkDocument
            {
                Title = ArtworkDocument.NextUntitledName(),
                Width = w,
                Height = h,
                BackgroundType = "White"
            };
            doc.Layers.Add(new PaintLayer(w, h, false) { BgraPixels = pixels });
            MainWindow.Current.NavigateToEditor(doc);
        }

        private void OnHistoryItemClick(object sender, ItemClickEventArgs e) { /* handled per-card */ }

        // ---------- Samples (bundled in Assets/Samples, referenced from .csproj) ----------

        private static readonly (string File, string Title)[] SamplePics = new[]
        {
            ("sample_sunset.png", "Sunset"),
            ("sample_ocean.png", "Ocean"),
            ("sample_forest.png", "Forest"),
            ("sample_abstract.png", "Abstract"),
        };

        private void LoadSamples()
        {
            SamplesGrid.Items.Clear();
            foreach (var (file, title) in SamplePics)
            {
                SamplesGrid.Items.Add(BuildSampleCard(file, title));
            }
        }

        private FrameworkElement BuildSampleCard(string file, string title)
        {
            var thumbBorder = new Border
            {
                Width = 220,
                Height = 200,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                BorderBrush = new SolidColorBrush(Col.Argb(30, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Child = new Image
                {
                    Source = new BitmapImage(new Uri($"ms-appx:///Assets/Samples/{file}")),
                    Stretch = Stretch.UniformToFill
                }
            };

            var label = new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(4, 8, 4, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var stack = new StackPanel();
            stack.Children.Add(thumbBorder);
            stack.Children.Add(label);

            var btn = new Button
            {
                Content = stack,
                Background = null,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 12, 12),
                Tag = file
            };
            btn.Click += async (s, e) => await OpenSampleAsync(file, title);
            return btn;
        }

        private async void OnSampleItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Button btn && btn.Tag is string file)
            {
                string title = file;
                foreach (var (f, t) in SamplePics)
                    if (f == file) title = t;
                await OpenSampleAsync(file, title);
            }
        }

        private async Task OpenSampleAsync(string file, string title)
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string path = System.IO.Path.Combine(baseDir, "Assets", "Samples", file);
                if (!System.IO.File.Exists(path)) return;
                var (pixels, w, h) = await ArtworkStore.DecodeImageAsync(path);
                var doc = new ArtworkDocument
                {
                    Title = $"{title} (Sample)",
                    Width = w,
                    Height = h,
                    BackgroundType = "White"
                };
                doc.Layers.Add(new PaintLayer(w, h, false) { BgraPixels = pixels });
                MainWindow.Current.NavigateToEditor(doc);
            }
            catch { /* ignore missing sample */ }
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            var panel = new StackPanel { Spacing = 2, MinWidth = 420 };
            var s = MainWindow.Current.Settings;

            panel.Children.Add(Section("Gesture"));
            panel.Children.Add(ToggleRow("Undo by Two Fingers Tap", s.UndoTwoFingerTap, v => s.UndoTwoFingerTap = v));
            panel.Children.Add(ToggleRow("Redo by Three Fingers Tap", s.RedoThreeFingerTap, v => s.RedoThreeFingerTap = v));
            panel.Children.Add(ToggleRow("Quick Eyedropper", s.QuickEyedropper, v => s.QuickEyedropper = v));
            panel.Children.Add(ToggleRow("View Rotation", s.ViewRotation, v => s.ViewRotation = v));
            panel.Children.Add(Section("Canvas"));
            panel.Children.Add(ToggleRow("Use Pressure Sensitivity", s.UsePressureSensitivity, v => s.UsePressureSensitivity = v));
            panel.Children.Add(ToggleRow("Grid", s.Grid, v => s.Grid = v));
            panel.Children.Add(ToggleRow("Manga Manuscript", s.MangaManuscript, v => s.MangaManuscript = v));

            var scroll = new ScrollViewer { MaxHeight = 460, Content = panel };
            var dialog = new ContentDialog
            {
                Title = "Settings",
                Content = scroll,
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        private FrameworkElement Section(string title)
        {
            return new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Col.Argb(255, 0x60, 0x60, 0x60)),
                Margin = new Thickness(0, 12, 0, 4)
            };
        }

        private FrameworkElement ToggleRow(string label, bool value, Action<bool> setter)
        {
            var grid = new Grid { Padding = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            });
            var toggle = new ToggleSwitch
            {
                IsOn = value,
                OnContent = string.Empty,
                OffContent = string.Empty,
                MinWidth = 90,
                Margin = new Thickness(0, -8, 0, -8)
            };
            toggle.Toggled += (s2, e2) =>
            {
                setter(toggle.IsOn);
                MainWindow.Current.SaveSettings();
            };
            grid.Children.Add(toggle);
            Grid.SetColumn(toggle, 1);
            return grid;
        }
    }
}
