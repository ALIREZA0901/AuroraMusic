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
    [ObservableProperty] private string status = "Changes are applied only when you click Apply.";

    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand OpenDataCommand { get; }

    public SettingsViewModel(SettingsService svc)
    {
        _svc = svc;
        var p = _svc.Current.Paths;

        BasePath = p.BasePath;
        WorkspacePath = p.WorkspacePath;
        InboxPath = p.InboxPath;
        CachePath = p.CachePath;
        CacheQuotaGb = Math.Max(1, _svc.Current.CacheQuotaBytesPc / (1024d * 1024 * 1024)).ToString("0");

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
    }

    private void Apply()
    {
        try
        {
            if (!long.TryParse(CacheQuotaGb, out var gb)) gb = 5;

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

            var s = _svc.Current with { Paths = paths, CacheQuotaBytesPc = gb * 1024L * 1024 * 1024 };
            _svc.Save(s);

            Directory.CreateDirectory(paths.WorkspacePath);
            Directory.CreateDirectory(paths.InboxPath);
            Directory.CreateDirectory(paths.DataPath);
            Directory.CreateDirectory(paths.CachePath);
            Directory.CreateDirectory(paths.CoversPath);
            Directory.CreateDirectory(paths.LyricsPath);
            Directory.CreateDirectory(paths.ArtistsPath);

            Status = "Applied. Restart Aurora Music to ensure all services use the new paths.";
        }
        catch (Exception ex)
        {
            Log.Error("Settings apply failed", ex);
            Status = "Apply failed. Check logs.";
            MessageBox.Show("Apply failed. Check logs for details.", "AuroraMusic", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
