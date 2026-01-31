# Aurora Music (Windows) — Source ZIP

This ZIP contains the full **Aurora Music** Windows desktop project:
- Music Library scan (My Music) + metadata read (TagLib#)
- Playback (NAudio)
- Browser (WebView2)
- Download Manager (queue + multipart if server supports Range)
- Settings v1 with **Apply**
- Donate page (TRON wallet QR + copy)

## Build Requirements (Windows)
1) Visual Studio 2022
   - Workload: **.NET Desktop Development**
2) .NET 8 SDK (pinned via `global.json`, comes with the workload)
3) WebView2 Runtime (usually already installed, required for the embedded browser)
   - https://developer.microsoft.com/microsoft-edge/webview2/

## How to run (easy)
Open `AuroraMusic.sln` in Visual Studio → press **F5**.

## Build an installable folder (no manual copy/paste)
Open PowerShell in:
`src\AuroraMusic.Desktop\`
and run:
```powershell
.\build-installer.ps1
```

Output:
`src\AuroraMusic.Desktop\bin\Release\net8.0-windows\win-x64\publish\`

You can move that folder anywhere and run `AuroraMusic.exe`.

> NOTE: This environment can't compile WPF binaries directly, so this ZIP ships as source + an auto-build script to generate the installable build on your Windows machine.
