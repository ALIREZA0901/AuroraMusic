using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using AuroraMusic.Core;
using AuroraMusic.Data;
using AuroraMusic.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuroraMusic.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly Db _db;
    private readonly LibraryScanService _scanner;
    private readonly PlaybackService _playback;
    private readonly Action<string,string> _nowPlaying;
    private readonly SettingsService _settings;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Timer _watchDebounce;

    [ObservableProperty] private string searchQuery = "title";
    [ObservableProperty] private ObservableCollection<Track> tracks = new();
    [ObservableProperty] private Track? selectedTrack;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string scanStatus = "";

    public IRelayCommand SearchCommand { get; }
    public IRelayCommand RescanCommand { get; }
    public IRelayCommand PlaySelectedCommand { get; }

    private readonly List<Track> _queue = new();
    private int _index = -1;

    public LibraryViewModel(Db db, LibraryScanService scanner, PlaybackService playback, SettingsService settings, Action<string,string> nowPlaying)
    {
        _db = db; _scanner = scanner; _playback = playback; _settings = settings; _nowPlaying = nowPlaying;

        _watchDebounce = new Timer(2000) { AutoReset = false };
        _watchDebounce.Elapsed += (_, __) =>
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!IsBusy)
                        _ = DoRescanAsync();
                });
            }
            catch (Exception ex)
            {
                Log.Warn($"LibraryViewModel: watch refresh failed. {ex.GetType().Name}: {ex.Message}");
            }
        };

        SearchCommand = new RelayCommand(DoSearch);
        RescanCommand = new RelayCommand(() =>
        {
            if (IsBusy) return;
            _ = DoRescanAsync();
        });
        PlaySelectedCommand = new RelayCommand(PlaySelected);

        try
        {
            Tracks = new ObservableCollection<Track>(_db.GetRecentTracks());
        }
        catch (Exception ex)
        {
            Log.Error("LibraryViewModel: failed to load recent tracks", ex);
            Tracks = new ObservableCollection<Track>();
        }

        StartWatchers();
    }

    private void DoSearch()
    {
        try
        {
            var q = string.IsNullOrWhiteSpace(SearchQuery) ? "title" : SearchQuery.Trim();
            Tracks = new ObservableCollection<Track>(_db.SearchTracks(q));
        }
        catch (Exception ex)
        {
            Log.Error("Library search failed", ex);
            MessageBox.Show("Search failed. Check logs for details.", "AuroraMusic", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DoRescanAsync()
    {
        IsBusy = true;
        ScanStatus = "Scanning My Music…";

        try
        {
            var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            var progress = new Progress<string>(p =>
            {
                // Keep it lightweight
                ScanStatus = $"Scanning: {System.IO.Path.GetFileName(p)}";
            });

            await Task.Run(() => _scanner.ScanFolders(new[] { music }, progress));

            Tracks = new ObservableCollection<Track>(_db.GetRecentTracks());
            ScanStatus = $"Scan done. Tracks: {Tracks.Count}";
        }
        catch (Exception ex)
        {
            Log.Error("Rescan failed", ex);
            ScanStatus = "Scan failed. See logs.";
            MessageBox.Show("Rescan failed. Check logs for details.", "AuroraMusic", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PlaySelected()
    {
        if (SelectedTrack is null) return;
        _queue.Clear();
        _queue.AddRange(Tracks);
        _index = _queue.FindIndex(t => t.Id == SelectedTrack.Id);
        if (_index < 0) _index = 0;
        PlayAt(_index);
    }

    private void PlayAt(int i)
    {
        if (i < 0 || i >= _queue.Count) return;
        _index = i;
        var t = _queue[i];

        try
        {
            _playback.PlayFile(t.FilePath);
            _nowPlaying(t.Title, t.Artist);
        }
        catch (Exception)
        {
            // PlaybackService already logged the exception
            MessageBox.Show("Playback failed. Check logs for details.", "AuroraMusic", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void PlayNext() => PlayAt(Math.Min(_index + 1, _queue.Count - 1));
    public void PlayPrevious() => PlayAt(Math.Max(_index - 1, 0));

    private void StartWatchers()
    {
        if (!_settings.Current.WatchFolders) return;

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            _settings.Current.Paths.InboxPath
        };

        foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r) && Directory.Exists(r)))
        {
            try
            {
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                };

                watcher.Created += OnLibraryChanged;
                watcher.Changed += OnLibraryChanged;
                watcher.Deleted += OnLibraryChanged;
                watcher.Renamed += OnLibraryChanged;

                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                Log.Warn($"LibraryViewModel: failed to watch '{root}'. {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private void OnLibraryChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsAudioPath(e.FullPath)) return;
        _watchDebounce.Stop();
        _watchDebounce.Start();
    }

    private bool IsAudioPath(string path)
    {
        try
        {
            var ext = Path.GetExtension(path);
            return _settings.Current.AllowedExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
