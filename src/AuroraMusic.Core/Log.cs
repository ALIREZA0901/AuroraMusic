using System;
using System.IO;
using System.Text;

namespace AuroraMusic.Core;

/// <summary>
/// Minimal file logger for AuroraMusic.
/// Logs to %LOCALAPPDATA%\AuroraMusic\Logs\auroramusic-YYYYMMDD.log by default.
/// Safe to call without Init(); it will self-initialize on first use.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static bool _initialized;
    private static string _logDir = "";
    private static string _logFile = "";

    public static string LogDirectory
    {
        get { EnsureInit(); return _logDir; }
    }

    public static string LogFilePath
    {
        get { EnsureInit(); return _logFile; }
    }

    /// <summary>
    /// Optional initialization (recommended). If baseDir is null, uses LocalAppData\AuroraMusic\Logs.
    /// </summary>
    public static void Init(string? baseDir = null)
    {
        lock (Gate)
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = string.IsNullOrWhiteSpace(baseDir)
                ? Path.Combine(local, "AuroraMusic")
                : baseDir.Trim();

            _logDir = Path.Combine(root, "Logs");
            Directory.CreateDirectory(_logDir);

            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            _logFile = Path.Combine(_logDir, $"auroramusic-{date}.log");
            _initialized = true;
        }
    }

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message) => Write("WARN", message, null);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void EnsureInit()
    {
        if (_initialized) return;
        try { Init(null); }
        catch
        {
            // Last resort: no exceptions from logger
            _initialized = true;
            _logDir = "";
            _logFile = "";
        }
    }

    private static void Write(string level, string message, Exception? ex)
    {
        EnsureInit();
        if (string.IsNullOrWhiteSpace(_logFile)) return;

        try
        {
            lock (Gate)
            {
                var sb = new StringBuilder();
                sb.Append(DateTime.UtcNow.ToString("o"))
                  .Append(' ')
                  .Append(level)
                  .Append(' ')
                  .Append(message);

                if (ex is not null)
                {
                    sb.AppendLine()
                      .Append(ex.ToString());
                }

                sb.AppendLine();

                File.AppendAllText(_logFile, sb.ToString());
            }
        }
        catch
        {
            // Never throw from logger
        }
    }
}
