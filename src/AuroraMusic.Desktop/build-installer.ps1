$ErrorActionPreference = "Stop"
Write-Host "Publishing AuroraMusic (self-contained win-x64)..." -ForegroundColor Cyan

dotnet restore

dotnet publish -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:DebugType=None `
  /p:DebugSymbols=false

Write-Host "Done." -ForegroundColor Green
Write-Host "Output folder:" -ForegroundColor Green
Write-Host (Join-Path $PSScriptRoot "bin\Release\net8.0-windows\win-x64\publish")
