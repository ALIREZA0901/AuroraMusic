using System;
using System.Windows;
using System.Windows.Controls;
using AuroraMusic.Core;

namespace AuroraMusic.Views;

public partial class BrowserView : UserControl
{
    private bool _initialized;

    public BrowserView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, __) => _initialized = false; // re-init when reloaded
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            if (Web.CoreWebView2 == null)
                await Web.EnsureCoreWebView2Async();

            Web.CoreWebView2.DownloadStarting += (_, args) =>
            {
                try
                {
                    if (DataContext is AuroraMusic.ViewModels.BrowserViewModel vm)
                        vm.HandleDownloadStarting(args.DownloadOperation.Uri);
                }
                catch (Exception ex)
                {
                    Log.Error("BrowserView: DownloadStarting handler failed", ex);
                }
            };
        }
        catch (Exception ex)
        {
            Log.Error("BrowserView: WebView2 initialization failed. Is WebView2 Runtime installed?", ex);
            MessageBox.Show(
                "WebView2 failed to initialize.\n\nPlease install 'Microsoft Edge WebView2 Runtime' and restart AuroraMusic.\n\nCheck logs for details.",
                "WebView2 Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            try { Web.IsEnabled = false; } catch { /* ignore */ }
        }
    }
}
