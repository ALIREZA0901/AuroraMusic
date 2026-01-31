using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AuroraMusic.Core;
using AuroraMusic.Data;
using AuroraMusic.Services;
using AuroraMusic.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuroraMusic;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly Db _db;
    private readonly LibraryScanService _scanner;
    private readonly PlaybackService _playback;
    private readonly DownloadService _downloads;

    [ObservableProperty] private object? currentView;
    [ObservableProperty] private string nowPlayingText = "Paused";

    public string PlayPauseText => _playback.IsPlaying ? "Pause" : "Play";

    public ICommand NavigateLibraryCommand { get; }
    public ICommand NavigateBrowserCommand { get; }
    public ICommand NavigateDownloadsCommand { get; }
    public ICommand NavigateSettingsCommand { get; }
    public ICommand NavigateDonateCommand { get; }
    public ICommand NavigateDiagnosticsCommand { get; }

    public ICommand PlayPauseCommand { get; }
    public ICommand PrevCommand { get; }
    public ICommand NextCommand { get; }

    private readonly ViewModels.LibraryViewModel _libraryVm;
    private readonly ViewModels.BrowserViewModel _browserVm;
    private readonly ViewModels.DownloadsViewModel _downloadsVm;
    private readonly ViewModels.SettingsViewModel _settingsVm;
    private readonly ViewModels.DonateViewModel _donateVm;
    private readonly ViewModels.DiagnosticsViewModel _diagnosticsVm;

    public MainWindowViewModel()
    {
        Log.Info("AuroraMusic starting…");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var settingsFile = Path.Combine(localAppData, "AuroraMusic", "settings.json");

        _settings = new SettingsService(settingsFile);

        EnsureDirectories(_settings.Current.Paths);

        _db = new Db(_settings.Current.Paths.DbPath);
        _db.Init();

        _scanner = new LibraryScanService(_db, _settings);
        _playback = new PlaybackService();
        _downloads = new DownloadService(_settings);

        _libraryVm = new ViewModels.LibraryViewModel(_db, _scanner, _playback, OnNowPlaying);
        _browserVm = new ViewModels.BrowserViewModel(_downloads);
        _downloadsVm = new ViewModels.DownloadsViewModel(_downloads);
        _settingsVm = new ViewModels.SettingsViewModel(_settings);
        _donateVm = new ViewModels.DonateViewModel();
        _diagnosticsVm = new ViewModels.DiagnosticsViewModel(_settings, settingsFile, _db);

        NavigateLibraryCommand = new RelayCommand(() => CurrentView = Bind(new LibraryView(), _libraryVm));
        NavigateBrowserCommand = new RelayCommand(() => CurrentView = Bind(new BrowserView(), _browserVm));
        NavigateDownloadsCommand = new RelayCommand(() => CurrentView = Bind(new DownloadsView(), _downloadsVm));
        NavigateSettingsCommand = new RelayCommand(() => CurrentView = Bind(new SettingsView(), _settingsVm));
        NavigateDonateCommand = new RelayCommand(() => CurrentView = Bind(new DonateView(), _donateVm));
        NavigateDiagnosticsCommand = new RelayCommand(() => CurrentView = Bind(new DiagnosticsView(), _diagnosticsVm));

        PlayPauseCommand = new RelayCommand(() =>
        {
            try
            {
                if (_playback.IsPlaying) _playback.Pause();
                else _playback.Resume();

                OnPropertyChanged(nameof(PlayPauseText));
            }
            catch (Exception ex)
            {
                Log.Error("PlayPause failed", ex);
                MessageBox.Show("Playback control failed. Check logs.", "AuroraMusic", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        });

        PrevCommand = new RelayCommand(() => _libraryVm.PlayPrevious());
        NextCommand = new RelayCommand(() => _libraryVm.PlayNext());

        // Default view
        CurrentView = Bind(new LibraryView(), _libraryVm);
    }

    private static void EnsureDirectories(AppPaths p)
    {
        try
        {
            Directory.CreateDirectory(p.BasePath);
            Directory.CreateDirectory(p.DataPath);
            Directory.CreateDirectory(p.CachePath);
            Directory.CreateDirectory(p.WorkspacePath);
            Directory.CreateDirectory(p.InboxPath);
            Directory.CreateDirectory(p.CoversPath);
            Directory.CreateDirectory(p.LyricsPath);
            Directory.CreateDirectory(p.ArtistsPath);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to create required directories (paths may be invalid or not writable).", ex);
            MessageBox.Show(
                "AuroraMusic could not create its required folders.\n\nOpen Diagnostics/Logs for details.",
                "AuroraMusic",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private static FrameworkElement Bind(FrameworkElement view, object vm)
    {
        view.DataContext = vm;
        return view;
    }

    private void OnNowPlaying(string title, string artist)
    {
        NowPlayingText = $"{title} — {artist}";
        OnPropertyChanged(nameof(PlayPauseText));
    }
}
