using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using iPaint.Services;
using iPaint.Views;

namespace iPaint
{
    public sealed partial class MainWindow : Window
    {
        public static MainWindow Current { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Current = this;

            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    AppWindow.SetIcon(iconPath);
                }
            }
            catch { }

            Title = "iPaint - Draw Anything";

            // WinUI 3 / Fluent app style
            SystemBackdrop = new MicaBackdrop();

            Settings = IbisSettings.Load();
            NavFrame.Navigate(typeof(HomePage));
        }

        public IbisSettings Settings { get; private set; }

        public void SaveSettings() => Settings.Save();

        public void NavigateToHome()
        {
            Title = "iPaint - Draw Anything";
            NavFrame.Navigate(typeof(HomePage));
        }

        public void NavigateToEditor(Models.ArtworkDocument doc)
        {
            NavFrame.Navigate(typeof(EditorPage), doc);
        }

        public void OpenArtwork(GalleryItem item)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                var doc = await ArtworkStore.LoadAsync(item);
                NavigateToEditor(doc);
            });
        }
    }
}
