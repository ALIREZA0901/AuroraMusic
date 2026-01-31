using System;
using System.Windows;
using AuroraMusic.Core;

namespace AuroraMusic;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        try
        {
            DataContext = new MainWindowViewModel();
        }
        catch (Exception ex)
        {
            Log.Error("Failed to initialize MainWindowViewModel", ex);
            MessageBox.Show(
                "AuroraMusic failed to start correctly.\n\nOpen the log folder to see details.",
                "AuroraMusic Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            try
            {
                var dir = Log.LogDirectory;
                if (!string.IsNullOrWhiteSpace(dir))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
            catch { /* ignore */ }
            Close();
        }
    }
}
