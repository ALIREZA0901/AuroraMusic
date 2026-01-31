using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AuroraMusic.Core;
using AuroraMusic.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuroraMusic.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _svc;

    [ObservableProperty] private string basePath;
    [ObservableProperty] private string workspacePath;
    [ObservableProperty] private string inboxPath;
    [ObservableProperty] private string cachePath;
    [ObservableProperty] private string cacheQuotaGb;
    [ObservableProperty] private string ignoreShortTracksSeconds;
    [ObservableProperty] private string ignoreFolderKeywords;
    [ObservableProperty] private string allowedExtensions;
    [ObservableProperty] private bool watchFolders;
    [ObservableProperty] private bool autoEnrichWhenOnline;
    [ObservableProperty] private string downloadPartsDefault;
    [ObservableProperty] private string maxConcurrentDownloads;
    [ObservableProperty] private string status = "Changes are applied only when you click Apply.";

    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand OpenDataCommand { get; }
    public IRelayCommand ClearCoversCacheCommand { get; }
    public IRelayCommand ClearLyricsCacheCommand { get; }
    public IRelayCommand ClearArtistsCacheCommand { get; }
    public IRelayCommand ClearAllCacheCommand { get; }

    public SettingsViewModel(SettingsService svc)
    {
        _svc = svc;
        var p = _svc.Current.Paths;

        BasePath = p.BasePath;
        WorkspacePath = p.WorkspacePath;
        InboxPath = p.InboxPath;
        CachePath = p.CachePath;
        CacheQuotaGb = Math.Max(1, _svc.Current.CacheQuotaBytesPc / (1024d * 1024 * 1024)).ToString("0");
        IgnoreShortTracksSeconds = Math.Max(1, _svc.Current.IgnoreShortTracksSeconds).ToString("0");
        IgnoreFolderKeywords = string.Join(", ", _svc.Current.IgnoreFolderKeywords);
        AllowedExtensions = string.Join(", ", _svc.Current.AllowedExtensions);
        WatchFolders = _svc.Current.WatchFolders;
        AutoEnrichWhenOnline = _svc.Current.AutoEnrichWhenOnline;
        DownloadPartsDefault = Math.Max(1, _svc.Current.DownloadPartsDefault).ToString("0");
        MaxConcurrentDownloads = Math.Max(1, _svc.Current.MaxConcurrentDownloads).ToString("0");

        ApplyCommand = new RelayCommand(Apply);
        OpenDataCommand = new RelayCommand(() =>
        {
            try
            {
                var data = _svc.Current.Paths.DataPath;
                Directory.CreateDirectory(data);
                Process.Start(new ProcessStartInfo("explorer.exe", data) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Error("OpenData failed", ex);
                MessageBox.Show("Failed to open Data folder. Check logs.", "AuroraMusic", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        });
        ClearCoversCacheCommand = new RelayCommand(() => ClearCacheDirectory(_svc.Current.Paths.CoversPath, "covers"));
        ClearLyricsCacheCommand = new RelayCommand(() => ClearCacheDirectory(_svc.Current.Paths.LyricsPath, "lyrics"));
        ClearArtistsCacheCommand = new RelayCommand(() => ClearCacheDirectory(_svc.Current.Paths.ArtistsPath, "artists"));
        ClearAllCacheCommand = new RelayCommand(() => ClearCacheDirectory(_svc.Current.Paths.CachePath, "all cache"));
    }

    private void Apply()
    {
        try
        {
            if (!long.TryParse(CacheQuotaGb, out var gb)) gb = 5;
            if (!int.TryParse(DownloadPartsDefault, out var partsDefault)) partsDefault = 6;
            if (!int.TryParse(MaxConcurrentDownloads, out var maxDownloads)) maxDownloads = 3;

            var paths = new AppPaths(
                BasePath: BasePath,
                WorkspacePath: WorkspacePath,
                InboxPath: InboxPath,
                DataPath: Path.Combine(BasePath, "Data"),
                DbPath: Path.Combine(BasePath, "Data", "aurora.db"),
                CachePath: CachePath,
                CoversPath: Path.Combine(CachePath, "Covers"),
                LyricsPath: Path.Combine(CachePath, "Lyrics"),
                ArtistsPath: Path.Combine(CachePath, "Artists")
            );

            if (!int.TryParse(IgnoreShortTracksSeconds, out var minSeconds)) minSeconds = 10;
            partsDefault = Math.Clamp(partsDefault, 1, 12);
            maxDownloads = Math.Max(1, maxDownloads);
            minSeconds = Math.Max(1, minSeconds);

            var keywords = ParseList(IgnoreFolderKeywords);
            var extensions = ParseExtensions(AllowedExtensions);

            var s = _svc.Current with
            {
                Paths = paths,
                CacheQuotaBytesPc = gb * 1024L * 1024 * 1024,
                WatchFolders = WatchFolders,
                AutoEnrichWhenOnline = AutoEnrichWhenOnline,
                DownloadPartsDefault = partsDefault,
                MaxConcurrentDownloads = maxDownloads,
                IgnoreShortTracksSeconds = minSeconds,
                IgnoreFolderKeywords = keywords,
                AllowedExtensions = extensions
            };
            _svc.Save(s);

            Directory.CreateDirectory(paths.WorkspacePath);
            Directory.CreateDirectory(paths.InboxPath);
            Directory.CreateDirectory(paths.DataPath);
            Directory.CreateDirectory(paths.CachePath);
            Directory.CreateDirectory(paths.CoversPath);
            Directory.CreateDirectory(paths.LyricsPath);
            Directory.CreateDirectory(paths.ArtistsPath);

            Status = "Applied. Restart Aurora Music to ensure all services use the new paths and download settings.";
        }
        catch (Exception ex)
        {
            Log.Error("Settings apply failed", ex);
            Status = "Apply failed. Check logs.";
            MessageBox.Show("Apply failed. Check logs for details.", "AuroraMusic", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearCacheDirectory(string? path, string label)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Status = $"Cache path for {label} is not set.";
                return;
            }

            if (!Directory.Exists(path))
            {
                Status = $"No {label} cache to clear.";
                return;
            }

            var removed = 0;
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                try
                {
                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                    removed++;
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to delete cache entry '{entry}'", ex);
                }
            }

            Status = removed == 0
                ? $"No {label} cache entries to remove."
                : $"Cleared {removed} {label} cache entr{(removed == 1 ? "y" : "ies")}.";
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to clear {label} cache.", ex);
            Status = $"Failed to clear {label} cache. Check logs.";
            MessageBox.Show($"Failed to clear {label} cache. Check logs for details.", "AuroraMusic", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string[] ParseList(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();
        return input.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string[] ParseExtensions(string? input)
    {
        var items = ParseList(input);
        if (items.Length == 0) return Array.Empty<string>();

        for (var i = 0; i < items.Length; i++)
        {
            var value = items[i].Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;
            items[i] = value.StartsWith('.') ? value.ToLowerInvariant() : $".{value.ToLowerInvariant()}";
        }

        return items;
    }
}
