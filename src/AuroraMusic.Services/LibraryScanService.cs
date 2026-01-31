using System.Security.Cryptography;
using AuroraMusic.Core;
using AuroraMusic.Data;
using TagLib;

namespace AuroraMusic.Services;

public sealed class LibraryScanService
{
    private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
    { ".mp3",".flac",".wav",".m4a",".aac",".ogg",".opus",".wma",".aiff",".aif" };

    private readonly Db _db;
    private readonly SettingsService _settings;

    public LibraryScanService(Db db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
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
                if (!AllowedExt.Contains(Path.GetExtension(f))) continue;
                progress?.Report(f);
                TryUpsert(f);
            }
        }
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
