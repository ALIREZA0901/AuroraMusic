using System.Security.Cryptography;
using AuroraMusic.Core;
using AuroraMusic.Data;
using TagLib;

namespace AuroraMusic.Services;

public sealed class LibraryScanService
{
    private readonly Db _db;
    private readonly SettingsService _settings;
    private readonly HashSet<string> _allowedExt;
    private readonly string[] _ignoreFolderKeywords;
    private readonly int _ignoreShortTracksSeconds;
    private readonly List<FileSystemWatcher> _watchers = new();

    public LibraryScanService(Db db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
        _allowedExt = new HashSet<string>(_settings.Current.AllowedExtensions, StringComparer.OrdinalIgnoreCase);
        _ignoreFolderKeywords = _settings.Current.IgnoreFolderKeywords;
        _ignoreShortTracksSeconds = _settings.Current.IgnoreShortTracksSeconds;
    }

    public void ScanFolders(IEnumerable<string> roots, IProgress<string>? progress = null)
    {
        foreach (var r in roots.Where(Directory.Exists))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(
                    r,
                    "*.*",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true
                    });
            }
            catch (Exception ex)
            {
                Log.Warn($"ScanFolders: failed to enumerate '{r}'. {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            foreach (var f in files)
            {
                if (ShouldIgnorePath(f)) continue;
                if (!_allowedExt.Contains(Path.GetExtension(f))) continue;
                progress?.Report(f);
                TryUpsert(f);
            }
        }
    }

    public void StartWatching(IEnumerable<string> roots)
    {
        StopWatching();

        foreach (var root in roots.Where(Directory.Exists))
        {
            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            watcher.Created += (_, args) => HandleFileChange(args.FullPath);
            watcher.Changed += (_, args) => HandleFileChange(args.FullPath);
            watcher.Renamed += (_, args) =>
            {
                try { _db.RemoveTrackByPath(args.OldFullPath); }
                catch (Exception ex) { Log.Warn($"Watcher delete failed for '{args.OldFullPath}'. {ex.Message}"); }
                HandleFileChange(args.FullPath);
            };
            watcher.Deleted += (_, args) =>
            {
                try { _db.RemoveTrackByPath(args.FullPath); }
                catch (Exception ex) { Log.Warn($"Watcher delete failed for '{args.FullPath}'. {ex.Message}"); }
            };

            _watchers.Add(watcher);
        }
    }

    public void StopWatching()
    {
        foreach (var watcher in _watchers)
        {
            try { watcher.EnableRaisingEvents = false; }
            catch { /* ignore */ }
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    public void TryUpsert(string filePath)
    {
        try
        {
            using var t = TagLib.File.Create(filePath);
            var title = string.IsNullOrWhiteSpace(t.Tag.Title) ? Path.GetFileNameWithoutExtension(filePath) : t.Tag.Title.Trim();
            var artist = (t.Tag.Performers?.FirstOrDefault() ?? "").Trim();
            var album = (t.Tag.Album ?? "").Trim();

            if (string.IsNullOrWhiteSpace(artist)) artist = "Unknown Artist";
            if (string.IsNullOrWhiteSpace(album)) album = "Unknown Album";

            var duration = t.Properties.Duration.TotalSeconds;
            if (_ignoreShortTracksSeconds > 0 && duration < _ignoreShortTracksSeconds)
                return;

            string? coverPath = null;
            if (t.Tag.Pictures is { Length: > 0 })
            {
                var pic = t.Tag.Pictures[0];
                coverPath = SaveCover(filePath, pic.Data.Data);
            }

            var hash = ComputeSha1(filePath);
            _db.UpsertTrack(filePath, title, artist, album, duration, coverPath, hash, false);
        }
        catch (Exception ex)
        {
            Log.Warn($"TryUpsert failed for '{filePath}'. {ex.GetType().Name}: {ex.Message}");
        }
    }

    private bool ShouldIgnorePath(string path)
    {
        if (_ignoreFolderKeywords.Length == 0) return false;

        foreach (var keyword in _ignoreFolderKeywords)
        {
            if (string.IsNullOrWhiteSpace(keyword)) continue;
            if (path.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void HandleFileChange(string path)
    {
        try
        {
            if (ShouldIgnorePath(path)) return;
            if (!_allowedExt.Contains(Path.GetExtension(path))) return;
            if (!File.Exists(path)) return;
            TryUpsert(path);
        }
        catch (Exception ex)
        {
            Log.Warn($"Watcher failed for '{path}'. {ex.GetType().Name}: {ex.Message}");
        }
    }

    private string? SaveCover(string filePath, byte[] bytes)
    {
        try
        {
            var coversDir = _settings.Current.Paths.CoversPath;
            Directory.CreateDirectory(coversDir);

            var name = Convert.ToHexString(SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(filePath))).ToLowerInvariant();
            var outPath = Path.Combine(coversDir, $"{name}.jpg");
            if (!System.IO.File.Exists(outPath))
                System.IO.File.WriteAllBytes(outPath, bytes);

            return outPath;
        }
        catch (Exception ex)
        {
            Log.Warn($"SaveCover failed for '{filePath}'. {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string ComputeSha1(string filePath)
    {
        using var sha1 = SHA1.Create();
        using var stream = System.IO.File.OpenRead(filePath);
        var hash = sha1.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
