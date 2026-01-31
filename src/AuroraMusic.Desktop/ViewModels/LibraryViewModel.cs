using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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

    public LibraryViewModel(Db db, LibraryScanService scanner, PlaybackService playback, Action<string,string> nowPlaying)
    {
        _db = db; _scanner = scanner; _playback = playback; _nowPlaying = nowPlaying;

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
}
