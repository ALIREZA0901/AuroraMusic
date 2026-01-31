using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuroraMusic.Services;
using AuroraMusic.Core;
using System.Windows;

namespace AuroraMusic.ViewModels;

public partial class BrowserViewModel : ObservableObject
{
    private readonly DownloadService _downloads;

    [ObservableProperty] private string address = "https://www.google.com";
    [ObservableProperty] private Uri browserUri = new Uri("https://www.google.com");

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
                BrowserUri = new Uri("https://www.google.com");
                Address = BrowserUri.ToString();
                return;
            }

            // If user typed a search phrase (spaces) or a non-URL token, use Google search.
            // This avoids UriFormatException and feels closer to a real browser.
            bool looksLikeSearch = input.Contains(' ') || (!input.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !input.Contains('.'));
            if (looksLikeSearch)
            {
                var q = Uri.EscapeDataString(input);
                var searchUrl = $"https://www.google.com/search?q={q}";
                BrowserUri = new Uri(searchUrl);
                Address = BrowserUri.ToString();
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
                 uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            {
                BrowserUri = uri;
                Address = BrowserUri.ToString();
                return;
            }

            // Fallback: search
            var fallbackQ = Uri.EscapeDataString(input);
            BrowserUri = new Uri($"https://www.google.com/search?q={fallbackQ}");
            Address = BrowserUri.ToString();
        });
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
