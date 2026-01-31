# AuroraMusic - Fixed Build (Fixed8)

This package contains fixes focused on "features not working / failing silently".

## Key fixes
- Added file logging: %LOCALAPPDATA%\AuroraMusic\Logs\auroramusic-YYYYMMDD.log
- Added global exception handlers (WPF Dispatcher + AppDomain + TaskScheduler) to prevent silent failures and open logs.
- Fixed default paths:
  - Old unsafe default "C:\AuroraMusic" replaced with per-user LocalAppData\AuroraMusic.
  - AutoEnrichWhenOnline default set to **false** (opt-in).
  - If an old non-writable BasePath is detected, settings migrate automatically.
- Library scan:
  - Removed silent catch blocks; logs scan/tag errors per file.
  - Rescan is now asynchronous (prevents UI freeze) and shows a scan status line.
- Downloads:
  - Better failure handling + reason stored in `LastError`.
  - HEAD failure fallback; explicit Cancelled status.
- Browser/WebView2:
  - WebView2 init guarded; shows a clear message if runtime is missing.
- Added Diagnostics page:
  - Shows path writability, DB health check, WebView2 runtime status, audio diagnostics, and log directory.

## How to run
Open `AuroraMusic.sln` in Visual Studio 2022, Build and Run.
