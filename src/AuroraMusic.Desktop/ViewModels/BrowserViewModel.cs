using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuroraMusic.Services;
using AuroraMusic.Core;
using System.Windows;

namespace AuroraMusic.ViewModels;

public partial class BrowserViewModel : ObservableObject
{
    private static readonly Uri HomeUri = new("https://www.google.com");
    private readonly DownloadService _downloads;

    [ObservableProperty] private string address = HomeUri.ToString();
    [ObservableProperty] private Uri browserUri = HomeUri;

    public IRelayCommand NavigateCommand { get; }

    public BrowserViewModel(DownloadService downloads)
    {
        _downloads = downloads;
        NavigateCommand = new RelayCommand(() =>
        {
            var input = (Address ?? string.Empty).Trim();

            // Empty input -> go home
            if (string.IsNullOrWhiteSpace(input))
            {
                SetBrowserUri(HomeUri);
                return;
            }

            // If user typed a search phrase (spaces) or a non-URL token, use Google search.
            // This avoids UriFormatException and feels closer to a real browser.
            bool looksLikeSearch = input.Contains(' ') || (!input.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !input.Contains('.'));
            if (looksLikeSearch)
            {
                SetBrowserUri(BuildSearchUri(input));
                return;
            }

            // Add scheme if missing
            var url = input;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            // Validate
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                 uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(uri.Host))
            {
                SetBrowserUri(uri);
                return;
            }

            // Fallback: search
            SetBrowserUri(BuildSearchUri(input));
        });
    }

    private static Uri BuildSearchUri(string query)
    {
        var q = Uri.EscapeDataString(query);
        var url = $"https://www.google.com/search?q={q}";
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : HomeUri;
    }

    private void SetBrowserUri(Uri uri)
    {
        BrowserUri = uri;
        Address = BrowserUri.ToString();
    }

    public async void HandleDownloadStarting(string url)
    {
        try
        {
            await _downloads.EnqueueAsync(url);
        }
        catch (Exception ex)
        {
            Log.Error("Enqueue download failed", ex);
            MessageBox.Show("Failed to enqueue download. Check logs.", "AuroraMusic", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
