# abose7ly — PUBG GameLoop Toolkit

Standalone Windows utility for safe PUBG GameLoop diagnostics and setup guidance. This repository is separate from the Commando library project.

## Prototype features
- Searches common GameLoop/Tencent and Tenstore-related install folders.
- Opens detected folders and Windows Temp for manual review.
- Provides conservative performance and rollback guidance.
- No aimbot, game-memory manipulation, anti-cheat bypass, or automatic deletion.
- The app itself runs without requesting administrator privileges.

## Build locally
Install the .NET 8 SDK and Inno Setup 6 on Windows, then run:

```powershell
dotnet restore abose7ly/abose7ly.csproj
dotnet build abose7ly/abose7ly.csproj -c Release
dotnet publish abose7ly/abose7ly.csproj -c Release -r win-x64 --self-contained true -o publish
```

The Inno Setup script is in `installer/abose7ly.iss`.

## Download a CI build
After a successful GitHub Actions run, open **Actions**, select **Build and test abose7ly for Windows**, and download `abose7ly-windows-build`. It contains the installer and published files.

## Known limits
This is an initial prototype, not a complete configuration repair suite. Detection checks common folders only. FPS/aspect ratio capabilities depend on the installed emulator, game, hardware, and current version.
