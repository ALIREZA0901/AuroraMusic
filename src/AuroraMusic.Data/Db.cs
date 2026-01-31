using AuroraMusic.Core;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AuroraMusic.Data;

public sealed class Db
{
    private readonly string _dbPath;
    public Db(string dbPath) => _dbPath = dbPath;

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        return new SqliteConnection($"Data Source={_dbPath}");
    }

    
        public void Init()
        {
            try
            {
                using var c = Open();
                c.Open();

                c.Execute(@"
        PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;

CREATE TABLE IF NOT EXISTS tracks(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  file_path TEXT NOT NULL UNIQUE,
  title TEXT NOT NULL,
  artist TEXT NOT NULL,
  album TEXT NOT NULL,
  duration_seconds REAL NOT NULL,
  cover_path TEXT NULL,
  is_duplicate INTEGER NOT NULL DEFAULT 0,
  hash TEXT NULL,
  date_added TEXT NOT NULL,
  last_modified TEXT NOT NULL
);

CREATE VIRTUAL TABLE IF NOT EXISTS tracks_fts USING fts5(
  title, artist, album, content='tracks', content_rowid='id'
);

CREATE TRIGGER IF NOT EXISTS tracks_ai AFTER INSERT ON tracks BEGIN
  INSERT INTO tracks_fts(rowid, title, artist, album)
  VALUES (new.id, new.title, new.artist, new.album);
END;

CREATE TRIGGER IF NOT EXISTS tracks_ad AFTER DELETE ON tracks BEGIN
  INSERT INTO tracks_fts(tracks_fts, rowid, title, artist, album)
  VALUES('delete', old.id, old.title, old.artist, old.album);
END;

CREATE TRIGGER IF NOT EXISTS tracks_au AFTER UPDATE ON tracks BEGIN
  INSERT INTO tracks_fts(tracks_fts, rowid, title, artist, album)
  VALUES('delete', old.id, old.title, old.artist, old.album);
  INSERT INTO tracks_fts(rowid, title, artist, album)
  VALUES (new.id, new.title, new.artist, new.album);
END;

CREATE INDEX IF NOT EXISTS idx_tracks_artist ON tracks(artist);
CREATE INDEX IF NOT EXISTS idx_tracks_album ON tracks(album);
CREATE INDEX IF NOT EXISTS idx_tracks_date_added ON tracks(date_added);
"
                );
            }
            catch (Exception ex)
            {
                Log.Error($"Db.Init failed for '{_dbPath}'", ex);
                throw;
            }
        }


public string HealthCheck()
{
    try
    {
        using var c = Open();
        c.Open();

        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT 1;";
        cmd.ExecuteScalar();

        using var cmd2 = c.CreateCommand();
        cmd2.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='tracks_fts';";
        var fts = cmd2.ExecuteScalar() as string;

        return fts is null ? "OK (fts table missing?)" : "OK (fts table present)";
    }
    catch (Exception ex)
    {
        Log.Error($"Db.HealthCheck failed for '{_dbPath}'", ex);
        return $"ERROR: {ex.GetType().Name}: {ex.Message}";
    }
}

public IEnumerable<Track> SearchTracks(string query, int limit = 200)
    {
        using var c = Open();
        c.Open();

        const string sql = @"
SELECT id Id, file_path FilePath, title Title, artist Artist, album Album,
       duration_seconds DurationSeconds, cover_path CoverPath,
       is_duplicate IsDuplicate, hash Hash
FROM tracks
WHERE id IN (SELECT rowid FROM tracks_fts WHERE tracks_fts MATCH @q)
LIMIT @limit;";

        return c.Query<Track>(sql, new { q = query, limit });
    }

    public IEnumerable<Track> GetRecentTracks(int limit = 200)
    {
        using var c = Open();
        c.Open();

        const string sql = @"
SELECT id Id, file_path FilePath, title Title, artist Artist, album Album,
       duration_seconds DurationSeconds, cover_path CoverPath,
       is_duplicate IsDuplicate, hash Hash
FROM tracks
ORDER BY date_added DESC
LIMIT @limit;";

        return c.Query<Track>(sql, new { limit });
    }

    public void UpsertTrack(string filePath, string title, string artist, string album, double durationSeconds, string? coverPath, string? hash, bool isDuplicate)
    {
        using var c = Open();
        c.Open();

        var now = DateTime.UtcNow.ToString("o");
        var lastMod = File.GetLastWriteTimeUtc(filePath).ToString("o");

        const string sql = @"
INSERT INTO tracks(file_path, title, artist, album, duration_seconds, cover_path, is_duplicate, hash, date_added, last_modified)
VALUES (@filePath, @title, @artist, @album, @durationSeconds, @coverPath, @isDuplicate, @hash, @now, @lastMod)
ON CONFLICT(file_path) DO UPDATE SET
  title=excluded.title,
  artist=excluded.artist,
  album=excluded.album,
  duration_seconds=excluded.duration_seconds,
  cover_path=excluded.cover_path,
  is_duplicate=excluded.is_duplicate,
  hash=excluded.hash,
  last_modified=excluded.last_modified;";

        c.Execute(sql, new { filePath, title, artist, album, durationSeconds, coverPath, isDuplicate = isDuplicate ? 1 : 0, hash, now, lastMod });
    }
}
