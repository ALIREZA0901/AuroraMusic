namespace AuroraMusic.Core;

public sealed class Track
{
    // Dapper materialization: needs parameterless ctor + settable properties
    public Track() { }

    public long Id { get; set; }
    public string FilePath { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public double DurationSeconds { get; set; }
    public string? CoverPath { get; set; }

    // Stored as INTEGER 0/1 in SQLite
    public long IsDuplicate { get; set; }
    public string? Hash { get; set; }

    public bool IsDuplicateBool => IsDuplicate != 0;
}

public record AppPaths(
    string BasePath,
    string WorkspacePath,
    string InboxPath,
    string DataPath,
    string DbPath,
    string CachePath,
    string CoversPath,
    string LyricsPath,
    string ArtistsPath
);

public record AppSettings(
    AppPaths Paths,
    long CacheQuotaBytesPc,
    bool AutoEnrichWhenOnline,
    bool WatchFolders,
    int DownloadPartsDefault,
    int MaxConcurrentDownloads
);
