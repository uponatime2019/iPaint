using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using iPaint.Services;

namespace iPaint
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();

            UnhandledException += (sender, args) =>
            {
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "iPaint");
                    Directory.CreateDirectory(Path.Combine(dir, "logs"));
                    File.AppendAllText(Path.Combine(dir, "logs", "app_session.txt"),
                        $"[{DateTime.Now:HH:mm:ss.fff}] UnhandledException: {args.Exception}\n\n");
                }
                catch { }
                args.Handled = true;
            };
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
