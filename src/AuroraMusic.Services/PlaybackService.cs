using AuroraMusic.Core;
using NAudio.Wave;

namespace AuroraMusic.Services;

public sealed class PlaybackService : IDisposable
{
    private IWavePlayer? _output;
    private AudioFileReader? _reader;

    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;

    public static string GetAudioDiagnostics()
    {
        try
        {
            return $"WaveOut devices: {WaveOut.DeviceCount}";
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
            _output.Init(_reader);
            _output.Play();
        }
        catch (Exception ex)
        {
            Log.Error($"PlaybackService.PlayFile failed for '{path}'", ex);
            Stop();
            throw;
        }
    }

    public void Pause() => _output?.Pause();
    public void Resume() => _output?.Play();

    public void Seek(TimeSpan position)
    {
        if (_reader is null) return;
        _reader.CurrentTime = position < TimeSpan.Zero ? TimeSpan.Zero :
                              position > _reader.TotalTime ? _reader.TotalTime :
                              position;
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

        try { _output?.Dispose(); }
        catch (Exception ex) { Log.Warn($"PlaybackService.Stop: output dispose failed: {ex.Message}"); }

        try { _reader?.Dispose(); }
        catch (Exception ex) { Log.Warn($"PlaybackService.Stop: reader dispose failed: {ex.Message}"); }

        _output = null;
        _reader = null;
    }

    public void Dispose() => Stop();
}
