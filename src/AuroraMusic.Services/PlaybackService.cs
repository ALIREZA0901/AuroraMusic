using System.Reflection;
using AuroraMusic.Core;
using NAudio.Wave;

namespace AuroraMusic.Services;

public sealed class PlaybackService : IDisposable
{
    private static readonly object DiagnosticsLock = new();
    private static string? _lastFilePath;
    private static string _lastState = PlaybackState.Stopped.ToString();
    private static TimeSpan? _lastPosition;
    private static TimeSpan? _lastDuration;

    private IWavePlayer? _output;
    private AudioFileReader? _reader;
    private string? _currentPath;
    private EventHandler<StoppedEventArgs>? _playbackStoppedHandler;

    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;

    public static string GetAudioDiagnostics()
    {
        try
        {
            string state;
            string? path;
            TimeSpan? position;
            TimeSpan? duration;

            lock (DiagnosticsLock)
            {
                state = _lastState;
                path = _lastFilePath;
                position = _lastPosition;
                duration = _lastDuration;
            }

            var deviceCount = GetWaveOutDeviceCount();

            return string.Join(Environment.NewLine, new[]
            {
                $"State: {state}",
                $"File: {path ?? "none"}",
                $"Position: {FormatTime(position)} / {FormatTime(duration)}",
                $"WaveOut devices: {deviceCount?.ToString() ?? "unknown"}"
            });
        }
        catch (Exception ex)
        {
            Log.Error("GetAudioDiagnostics failed", ex);
            return $"Audio error: {ex.GetType().Name}: {ex.Message}";
        }
    }

    public void PlayFile(string path)
    {
        try
        {
            Stop();
            _reader = new AudioFileReader(path);
            _output = new WaveOutEvent();
            _playbackStoppedHandler = (_, __) =>
            {
                _currentPath = null;
                UpdateDiagnosticsSnapshot();
            };
            _output.PlaybackStopped += _playbackStoppedHandler;
            _output.Init(_reader);
            _output.Play();
            _currentPath = path;
            UpdateDiagnosticsSnapshot();
        }
        catch (Exception ex)
        {
            Log.Error($"PlaybackService.PlayFile failed for '{path}'", ex);
            Stop();
            throw;
        }
    }

    public void Pause()
    {
        _output?.Pause();
        UpdateDiagnosticsSnapshot();
    }

    public void Resume()
    {
        _output?.Play();
        UpdateDiagnosticsSnapshot();
    }

    public void Seek(TimeSpan position)
    {
        if (_reader is null) return;
        _reader.CurrentTime = position < TimeSpan.Zero ? TimeSpan.Zero :
                              position > _reader.TotalTime ? _reader.TotalTime :
                              position;
        UpdateDiagnosticsSnapshot();
    }

    public void SetVolume(float volume01)
    {
        if (_reader is null) return;
        _reader.Volume = Math.Clamp(volume01, 0f, 1f);
    }

    public void Stop()
    {
        try { _output?.Stop(); }
        catch (Exception ex) { Log.Warn($"PlaybackService.Stop: {_output?.GetType().Name} stop failed: {ex.Message}"); }

        if (_output is not null && _playbackStoppedHandler is not null)
        {
            _output.PlaybackStopped -= _playbackStoppedHandler;
        }
        _playbackStoppedHandler = null;

        try { _output?.Dispose(); }
        catch (Exception ex) { Log.Warn($"PlaybackService.Stop: output dispose failed: {ex.Message}"); }

        try { _reader?.Dispose(); }
        catch (Exception ex) { Log.Warn($"PlaybackService.Stop: reader dispose failed: {ex.Message}"); }

        _output = null;
        _reader = null;
        _currentPath = null;
        UpdateDiagnosticsSnapshot();
    }

    public void Dispose() => Stop();

    private void UpdateDiagnosticsSnapshot()
    {
        lock (DiagnosticsLock)
        {
            _lastFilePath = _currentPath;
            _lastState = _output?.PlaybackState.ToString() ?? PlaybackState.Stopped.ToString();
            _lastPosition = _reader?.CurrentTime;
            _lastDuration = _reader?.TotalTime;
        }
    }

    private static int? GetWaveOutDeviceCount()
    {
        try
        {
            var type = Type.GetType("NAudio.Wave.WaveOut, NAudio.Wave");
            var property = type?.GetProperty("DeviceCount", BindingFlags.Public | BindingFlags.Static);
            if (property?.GetValue(null) is int count)
                return count;
        }
        catch
        {
            // Ignore device count failures
        }

        return null;
    }

    private static string FormatTime(TimeSpan? value)
    {
        return value.HasValue ? value.Value.ToString(@"hh\:mm\:ss") : "n/a";
    }
}
