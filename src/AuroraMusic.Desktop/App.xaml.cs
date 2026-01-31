using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using AuroraMusic.Core;

namespace AuroraMusic;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Initialize logging as early as possible
        Log.Init();

        // Global exception handlers (no silent failures)
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                if (args.ExceptionObject is Exception ex)
                    Log.Error("AppDomain.CurrentDomain.UnhandledException", ex);
                else
                    Log.Error($"AppDomain.CurrentDomain.UnhandledException (non-exception): {args.ExceptionObject}");
            }
            catch { /* ignore */ }
        };

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                Log.Error("Application.DispatcherUnhandledException", args.Exception);
                MessageBox.Show(
                    "AuroraMusic hit an unexpected error.\n\nOpen the log folder to see details.",
                    "AuroraMusic Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                TryOpenLogsFolder();
            }
            catch { /* ignore */ }
            finally
            {
                // Prevent hard crash; keep app alive when possible
                args.Handled = true;
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            try { Log.Error("TaskScheduler.UnobservedTaskException", args.Exception); }
            catch { /* ignore */ }
            finally { args.SetObserved(); }
        };

        base.OnStartup(e);
    }

    private static void TryOpenLogsFolder()
    {
        try
        {
            var dir = Log.LogDirectory;
            if (string.IsNullOrWhiteSpace(dir)) return;
            Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }
}
