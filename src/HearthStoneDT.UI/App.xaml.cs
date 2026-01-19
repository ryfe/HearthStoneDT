using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using HearthStoneDT.UI.Overlay;

namespace HearthStoneDT.UI
{
    public partial class App : Application
    {
        public static OverlayController Overlays { get; private set; } = new OverlayController();

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // ✅ UI 스레드 예외
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            // ✅ 비-UI 스레드 예외
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            Overlays.Initialize();

            var main = new HearthStoneDT.UI.Views.MainWindow();
            main.Show();
        }

        private static void LogException(Exception ex)
        {
            var path = @"C:\HearthStoneDT\crash.txt";
            File.AppendAllText(path, $"[{DateTime.Now}] {ex}\r\n\r\n");
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            MessageBox.Show(e.Exception.ToString(), "Crash");
            e.Handled = true; // 일단 안 죽게
        }

        private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LogException(ex);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Overlays.Dispose();
            base.OnExit(e);
        }
    }
}
