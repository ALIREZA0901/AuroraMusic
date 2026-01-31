using System.Text.Json;
using AuroraMusic.Core;

namespace AuroraMusic.Services;

public sealed class SettingsService
{
    private readonly string _settingsFile;
    public AppSettings Current { get; private set; }

    public SettingsService(string settingsFile)
    {
        _settingsFile = settingsFile;
        Current = LoadOrCreateDefault(settingsFile);
        Current = NormalizeSettings(Current);

        // If user has an old default path that requires admin (e.g., C:\AuroraMusic) or a non-writable path,
        // migrate to a safe per-user location under LocalAppData.
        try
        {
            Current = NormalizeSettings(MigrateIfNeeded(Current));
            // Ensure settings file exists so next startup is consistent
            Save(Current);
        }
        catch (Exception ex)
        {
            Log.Error("SettingsService failed during migration/save. App may still run with in-memory defaults.", ex);
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFile, json);
            Current = settings;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to save settings to '{_settingsFile}'", ex);
            throw;
        }
    }

    private static AppSettings LoadOrCreateDefault(string settingsFile)
    {
        try
        {
            if (File.Exists(settingsFile))
            {
                var json = File.ReadAllText(settingsFile);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                if (s is not null) return s;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load settings from '{settingsFile}', falling back to defaults.", ex);
        }

        // Safe per-user default location
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var basePath = Path.Combine(local, "AuroraMusic");
        var cachePath = Path.Combine(basePath, "Cache");

        var paths = BuildPaths(basePath, cachePath);

        return new AppSettings(
            Paths: paths,
            CacheQuotaBytesPc: 5L * 1024 * 1024 * 1024,
            OnlineEnabled: true,
            AiAssistEnabled: true,
            AutoEnrichWhenOnline: false, // opt-in only
            WatchFolders: true,
            DownloadPartsDefault: 6,
            MaxConcurrentDownloads: 3,
            IgnoreShortTracksSeconds: 10,
            IgnoreFolderKeywords: new[] { "voice", "recording", "memo" },
            AllowedExtensions: new[] { ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".aiff", ".aif" }
        );
    }

    private static AppPaths BuildPaths(string basePath, string cachePath)
    {
        return new AppPaths(
            BasePath: basePath,
            WorkspacePath: Path.Combine(basePath, "Workspace"),
            InboxPath: Path.Combine(basePath, "Inbox"),
            DataPath: Path.Combine(basePath, "Data"),
            DbPath: Path.Combine(basePath, "Data", "aurora.db"),
            CachePath: cachePath,
            CoversPath: Path.Combine(cachePath, "Covers"),
            LyricsPath: Path.Combine(cachePath, "Lyrics"),
            ArtistsPath: Path.Combine(cachePath, "Artists")
        );
    }

    private static AppSettings MigrateIfNeeded(AppSettings s)
    {
        var basePath = s.Paths.BasePath ?? string.Empty;

        // Old unsafe default path
        bool looksLikeOldDefault = basePath.Equals(@"C:\AuroraMusic", StringComparison.OrdinalIgnoreCase);

        // If base path is not writable, migrate.
        bool writable = IsWritableDirectory(basePath);

        if (!looksLikeOldDefault && writable)
        {
            // Also ensure opt-in default is respected if missing
            return s with { AutoEnrichWhenOnline = s.AutoEnrichWhenOnline };
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var newBase = Path.Combine(local, "AuroraMusic");
        var newCache = Path.Combine(newBase, "Cache");

        try { Directory.CreateDirectory(newBase); } catch { /* handled by IsWritableDirectory later */ }

        Log.Warn($"Migrating BasePath from '{basePath}' to '{newBase}' (old default or not writable).");

        var newPaths = BuildPaths(newBase, newCache);

        // Preserve non-path settings, force AutoEnrich to remain opt-in
        return s with
        {
            Paths = newPaths,
            OnlineEnabled = s.OnlineEnabled,
            AiAssistEnabled = s.AiAssistEnabled,
            AutoEnrichWhenOnline = false
        };
    }

    private static AppSettings NormalizeSettings(AppSettings s)
    {
        var ignoreFolders = s.IgnoreFolderKeywords ?? Array.Empty<string>();
        var allowedExt = s.AllowedExtensions ?? Array.Empty<string>();

        if (ignoreFolders.Length == 0)
            ignoreFolders = new[] { "voice", "recording", "memo" };

        if (allowedExt.Length == 0)
            allowedExt = new[] { ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".aiff", ".aif" };

        var minSeconds = s.IgnoreShortTracksSeconds <= 0 ? 10 : s.IgnoreShortTracksSeconds;

        return s with
        {
            OnlineEnabled = s.OnlineEnabled,
            AiAssistEnabled = s.AiAssistEnabled,
            IgnoreFolderKeywords = ignoreFolders,
            AllowedExtensions = allowedExt,
            IgnoreShortTracksSeconds = minSeconds
        };
    }

    private static bool IsWritableDirectory(string? dir)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dir)) return false;
            Directory.CreateDirectory(dir);

            var test = Path.Combine(dir, $".write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(test, "ok");
            File.Delete(test);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
