using System;
using System.Diagnostics;
using System.IO;
using AuroraMusic.Core;
using AuroraMusic.Data;
using AuroraMusic.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Web.WebView2.Core;

namespace AuroraMusic.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly string _settingsFile;
    private readonly Db _db;

    [ObservableProperty] private string settingsFilePath = "";
    [ObservableProperty] private string basePath = "";
    [ObservableProperty] private string workspacePath = "";
    [ObservableProperty] private string inboxPath = "";
    [ObservableProperty] private string dataPath = "";
    [ObservableProperty] private string dbPath = "";
    [ObservableProperty] private string cachePath = "";
    [ObservableProperty] private string logDirectory = "";

    [ObservableProperty] private string pathsStatus = "";
    [ObservableProperty] private string dbStatus = "";
    [ObservableProperty] private string webView2Status = "";
    [ObservableProperty] private string audioStatus = "";

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand OpenLogsCommand { get; }
    public IRelayCommand OpenDataCommand { get; }

    public DiagnosticsViewModel(SettingsService settings, string settingsFile, Db db)
    {
        _settings = settings;
        _settingsFile = settingsFile;
        _db = db;

        RefreshCommand = new RelayCommand(Refresh);
        OpenLogsCommand = new RelayCommand(() =>
        {
            try
            {
                var dir = Log.LogDirectory;
                if (!string.IsNullOrWhiteSpace(dir))
                    Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
            catch { /* ignore */ }
        });

        OpenDataCommand = new RelayCommand(() =>
        {
            try
            {
                var dir = _settings.Current.Paths.DataPath;
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
            catch { /* ignore */ }
        });

        Refresh();
    }

    private void Refresh()
    {
        try
        {
            var p = _settings.Current.Paths;

            SettingsFilePath = _settingsFile;
            BasePath = p.BasePath;
            WorkspacePath = p.WorkspacePath;
            InboxPath = p.InboxPath;
            DataPath = p.DataPath;
            DbPath = p.DbPath;
            CachePath = p.CachePath;
            LogDirectory = Log.LogDirectory;

            PathsStatus = BuildPathsStatus(p);
            DbStatus = _db.HealthCheck();
            WebView2Status = CheckWebView2();
            AudioStatus = PlaybackService.GetAudioDiagnostics();
        }
        catch (Exception ex)
        {
            Log.Error("DiagnosticsViewModel.Refresh failed", ex);
        }
    }

    private static string BuildPathsStatus(AppPaths p)
    {
        static string W(string path) => $"{path}  =>  {(IsWritable(path) ? "OK" : "NOT WRITABLE")}";

        return string.Join(Environment.NewLine, new[]
        {
            W(p.BasePath),
            W(p.WorkspacePath),
            W(p.InboxPath),
            W(p.DataPath),
            W(p.CachePath),
            W(p.CoversPath),
            W(p.LyricsPath),
            W(p.ArtistsPath),
        });
    }

    private static bool IsWritable(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            Directory.CreateDirectory(path);

            var test = Path.Combine(path, $".write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(test, "ok");
            File.Delete(test);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string CheckWebView2()
    {
        try
        {
            var v = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return string.IsNullOrWhiteSpace(v) ? "Not installed" : $"Installed: {v}";
        }
        catch (Exception ex)
        {
            return $"Not available: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
