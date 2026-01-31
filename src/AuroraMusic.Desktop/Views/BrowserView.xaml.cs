using System;
using System.Windows;
using System.Windows.Controls;
using AuroraMusic.Core;
using Microsoft.Web.WebView2.Core;

namespace AuroraMusic.Views;

public partial class BrowserView : UserControl
{
    private bool _initialized;
    private EventHandler<CoreWebView2DownloadStartingEventArgs>? _downloadStartingHandler;

    public BrowserView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            if (Web.CoreWebView2 == null)
                await Web.EnsureCoreWebView2Async();

            if (Web.CoreWebView2 == null)
                throw new InvalidOperationException("WebView2 failed to initialize.");

            _downloadStartingHandler = (_, args) =>
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
            Web.CoreWebView2.DownloadStarting += _downloadStartingHandler;
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

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is not null && _downloadStartingHandler is not null)
        {
            Web.CoreWebView2.DownloadStarting -= _downloadStartingHandler;
        }

        _downloadStartingHandler = null;
        _initialized = false; // re-init when reloaded
    }
}
