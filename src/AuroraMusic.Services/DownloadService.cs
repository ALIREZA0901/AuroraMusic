using System.Net.Http.Headers;
using System.Linq;
using AuroraMusic.Core;

namespace AuroraMusic.Services;

public record DownloadItem(
    Guid Id,
    string Url,
    string FileName,
    string SavePath,
    long? TotalBytes,
    long DownloadedBytes,
    string Status,
    DateTime CreatedUtc,
    string? LastError
);

public sealed class DownloadService
{
    private readonly SettingsService _settings;
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    private readonly SemaphoreSlim _concurrency;
    private readonly List<DownloadItem> _items = new();
    private readonly object _itemsLock = new();
    public IReadOnlyList<DownloadItem> Items
    {
        get
        {
            lock (_itemsLock)
            {
                return _items.ToList();
            }
        }
    }

    public event Action? ItemsChanged;

    public DownloadService(SettingsService settings)
    {
        _settings = settings;
        _concurrency = new SemaphoreSlim(Math.Max(1, _settings.Current.MaxConcurrentDownloads));
    }

    public async Task EnqueueAsync(string url, string? suggestedFileName = null, CancellationToken ct = default)
    {
        var workspace = _settings.Current.Paths.WorkspacePath;

        try { Directory.CreateDirectory(workspace); }
        catch (Exception ex)
        {
            Log.Error($"DownloadService: failed to create workspace '{workspace}'", ex);
            throw;
        }

        var fileName = suggestedFileName ?? GuessFileName(url);
        var savePath = GetUniqueSavePath(workspace, SanitizeFileName(fileName));

        var item = new DownloadItem(Guid.NewGuid(), url, fileName, savePath, null, 0, "Queued", DateTime.UtcNow, null);
        lock (_itemsLock)
        {
            _items.Insert(0, item);
        }
        ItemsChanged?.Invoke();

        _ = Task.Run(() => RunDownloadAsync(item.Id, ct), ct);
        await Task.CompletedTask;
    }

    private async Task RunDownloadAsync(Guid id, CancellationToken ct)
    {
        await _concurrency.WaitAsync(ct);
        try
        {
            if (!TryGetItem(id, out var current)) return;

            UpdateById(id, item => item with { Status = "Preparing", LastError = null });

            // Try HEAD to detect size/range support (fallback safely if not supported).
            long? len = null;
            bool acceptRanges = false;

            try
            {
                using var head = new HttpRequestMessage(HttpMethod.Head, current.Url);
                using var headResp = await _http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct);
                len = headResp.Content.Headers.ContentLength;
                acceptRanges = headResp.Headers.AcceptRanges.Any(r => r.Equals("bytes", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Log.Warn($"DownloadService: HEAD failed for '{current.Url}'. Falling back to GET. {ex.GetType().Name}: {ex.Message}");
            }

            UpdateById(id, item => item with { TotalBytes = len });

            if (acceptRanges && len is > 5_000_000)
            {
                UpdateById(id, item => item with { Status = "Downloading (multipart)" });
                await DownloadMultipartAsync(id, ct);
            }
            else
            {
                UpdateById(id, item => item with { Status = "Downloading" });
                await DownloadSingleAsync(id, ct);
            }

            UpdateById(id, item => item with { Status = "Done", LastError = null });
        }
        catch (OperationCanceledException)
        {
            UpdateById(id, item => item with { Status = "Cancelled", LastError = "Cancelled" });
        }
        catch (Exception ex)
        {
            Log.Error($"DownloadService: download failed for id={id}", ex);
            UpdateById(id, item => item with { Status = "Failed", LastError = ex.Message });
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private async Task DownloadSingleAsync(Guid id, CancellationToken ct)
    {
        if (!TryGetItem(id, out var item)) return;

        var completed = false;
        try
        {
            using var resp = await _http.GetAsync(item.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? item.TotalBytes;
            UpdateById(id, existing => existing with { TotalBytes = total });

            await using var input = await resp.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(item.SavePath);

            var buf = new byte[81920];
            long readTotal = 0;

            while (true)
            {
                var read = await input.ReadAsync(buf, ct);
                if (read == 0) break;

                await output.WriteAsync(buf.AsMemory(0, read), ct);
                readTotal += read;

                // idx may shift if list changes; re-find by id
                UpdateById(id, existing => existing with { DownloadedBytes = readTotal });
            }

            completed = true;
        }
        finally
        {
            if (!completed)
                TryDeleteFile(item.SavePath);
        }
    }

    private async Task DownloadMultipartAsync(Guid id, CancellationToken ct)
    {
        if (!TryGetItem(id, out var item)) return;
        if (item.TotalBytes is null)
        {
            await DownloadSingleAsync(id, ct);
            return;
        }

        var parts = Math.Clamp(_settings.Current.DownloadPartsDefault, 4, 12);
        var total = item.TotalBytes.Value;

        var tempDir = Path.Combine(Path.GetDirectoryName(item.SavePath)!, ".aurora_parts");
        Directory.CreateDirectory(tempDir);

        var ranges = SplitRanges(total, parts).ToList();
        var partFiles = ranges.Select((_, i) => Path.Combine(tempDir, $"{item.Id}_{i}.part")).ToArray();

        var completed = false;
        try
        {
            await Task.WhenAll(ranges.Select((r, i) => DownloadRangeAsync(item.Url, partFiles[i], r.start, r.end, ct)));

            await using var outStream = File.Create(item.SavePath);
            foreach (var pf in partFiles)
            {
                await using var ps = File.OpenRead(pf);
                await ps.CopyToAsync(outStream, ct);
            }

            UpdateById(id, existing => existing with { DownloadedBytes = total });
            completed = true;
        }
        finally
        {
            if (!completed)
                TryDeleteFile(item.SavePath);

            foreach (var pf in partFiles)
            {
                try { File.Delete(pf); }
                catch (Exception ex) { Log.Warn($"DownloadService: failed to delete part '{pf}'. {ex.Message}"); }
            }

            try { Directory.Delete(tempDir, true); }
            catch (Exception ex) { Log.Warn($"DownloadService: failed to delete tempDir '{tempDir}'. {ex.Message}"); }
        }
    }

    private async Task DownloadRangeAsync(string url, string path, long start, long end, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Range = new RangeHeaderValue(start, end);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var input = await resp.Content.ReadAsStreamAsync(ct);
        await using var output = File.Create(path);

        var buf = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buf, ct);
            if (read == 0) break;
            await output.WriteAsync(buf.AsMemory(0, read), ct);
        }
    }

    private static IEnumerable<(long start, long end)> SplitRanges(long total, int parts)
    {
        var chunk = total / parts;
        long s = 0;

        for (int i = 0; i < parts; i++)
        {
            var e = (i == parts - 1) ? (total - 1) : (s + chunk - 1);
            yield return (s, e);
            s = e + 1;
        }
    }

    private void UpdateById(Guid id, Func<DownloadItem, DownloadItem> update)
    {
        var changed = false;
        lock (_itemsLock)
        {
            var idx = _items.FindIndex(x => x.Id == id);
            if (idx < 0 || idx >= _items.Count) return;
            _items[idx] = update(_items[idx]);
            changed = true;
        }
        if (changed)
            ItemsChanged?.Invoke();
    }

    private bool TryGetItem(Guid id, out DownloadItem item)
    {
        lock (_itemsLock)
        {
            var idx = _items.FindIndex(x => x.Id == id);
            if (idx < 0 || idx >= _items.Count)
            {
                item = default!;
                return false;
            }

            item = _items[idx];
            return true;
        }
    }

    private string GetUniqueSavePath(string workspace, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var candidate = Path.Combine(workspace, fileName);
        var index = 1;

        while (true)
        {
            if (!File.Exists(candidate) && !IsPathInQueue(candidate))
                return candidate;

            candidate = Path.Combine(workspace, $"{name} ({index}){ext}");
            index++;
        }
    }

    private bool IsPathInQueue(string path)
    {
        lock (_itemsLock)
        {
            return _items.Any(i => string.Equals(i.SavePath, path, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warn($"DownloadService: failed to delete '{path}'. {ex.Message}");
        }
    }

    private static string GuessFileName(string url)
    {
        try
        {
            var u = new Uri(url);
            var last = Path.GetFileName(u.LocalPath);
            return string.IsNullOrWhiteSpace(last) ? "download.bin" : last;
        }
        catch
        {
            return "download.bin";
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
    }
}
