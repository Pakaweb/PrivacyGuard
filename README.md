# PrivacyGuard

Unified Privacy & Telemetry Control Dashboard for Windows 10 (1809+) and Windows 11.

PrivacyGuard is a WinUI 3 desktop app that shows what Windows is collecting and lets you change the most common telemetry, advertising, and related privacy settings from one place. Every mutation is confirmed, backed up, logged, and reversible. Critical operating-system services are never touched.

## Features (MVP)

- **Dashboard** with a privacy score and green / yellow / red status for telemetry, DiagTrack, dmwappushservice, advertising ID, activity history, Cortana, Copilot, feedback, and tailored experiences
- **One-click profiles:** Recommended Privacy, Maximum Privacy, Balanced, Restore Default
- **Per-setting toggles** routed through a single `PrivacyService`
- **Automatic restore points** written to SQLite before changes
- **Change history** with per-item revert and full restore-point replay
- **Elevation-aware UI** (standard user can inspect; administrator can apply machine-wide policy)
- **Mica / Acrylic backdrop**, dark and light themes, Settings page for app preferences

## Requirements

- Windows 10 version 1809 (build 17763) or later, or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) 17.8+ with the **Windows application development** workload
  - Windows App SDK C# templates
  - Windows 10/11 SDK (10.0.19041 or newer)

This solution is **unpackaged** (`WindowsPackageType=None`) and **self-contained** for the Windows App SDK, so you do not need a separate runtime installer on the machine that builds and runs it.

> Build and run on Windows. The project cannot be compiled on macOS or Linux because it depends on WinUI 3 and Windows-only APIs. On a Mac you can open `preview/index.html` in a browser to review the UI. That preview does not read or change Windows settings.

## Build and run

From a Developer Command Prompt or PowerShell:

```powershell
git clone <your-repo-url> PrivacyGuard
cd PrivacyGuard
dotnet restore PrivacyGuard.sln
dotnet build PrivacyGuard.sln -c Debug -p:Platform=x64
dotnet run --project src\PrivacyGuard\PrivacyGuard.csproj -c Debug -p:Platform=x64
```

Or open `PrivacyGuard.sln` in Visual Studio, choose **x64**, and press F5.

If you prefer a packaged MSIX later, add a `Package.appxmanifest` and set `WindowsPackageType` to `MSIX`. Elevation is simpler in the current unpackaged model (UAC *Restart as admin*).

## Safety model

PrivacyGuard is designed so a mistake is recoverable and a bug cannot disable core Windows:

1. **Confirmation** — system changes always show a dialog that lists old → new values and side effects.
2. **Backup first** — a restore point of the affected settings is stored in `%LocalAppData%\PrivacyGuard\privacyguard.db`.
3. **History** — each write is an audit row that can be reverted individually.
4. **Allowlist** — the only services that can be stopped or have their start type changed are `DiagTrack` and `dmwappushservice`. A second block list refuses well-known critical services.
5. **No silent elevation** — the process starts `asInvoker`. Machine policy and service changes require an explicit UAC relaunch.
6. **SKU honesty** — telemetry level *Security (0)* is documented as Enterprise/Education-only. On Home/Pro, Windows treats it as Basic.

Do not run untrusted copies of this app elevated. Review the registry paths in `PrivacyRegistryPaths` before distributing internally.

## Architecture

```
Views  →  ViewModels  →  IPrivacyService  →  RegistryHelper / WindowsServiceHelper
                              ↓
                     BackupService + ChangeHistoryService (SQLite)
```

| Area | Role |
| --- | --- |
| `Services/PrivacyService.cs` | Only type that mutates Windows privacy state |
| `Helpers/RegistryHelper.cs` | 64-bit registry read/write/delete |
| `Helpers/WindowsServiceHelper.cs` | Allowlisted service status and start type |
| `Services/BackupService.cs` | Restore points |
| `Services/ChangeHistoryService.cs` | Audit log |
| `Services/ElevationService.cs` | Admin detection and UAC restart |

### Registry keys used

| Setting | Location |
| --- | --- |
| Telemetry | `HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection\AllowTelemetry` and `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection\AllowTelemetry` |
| Advertising ID | `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo\Enabled` |
| Activity history | `HKLM\SOFTWARE\Policies\Microsoft\Windows\System\PublishUserActivities`, `UploadUserActivities` |
| Cortana | `HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search\AllowCortana` |
| Copilot | `HKLM` / `HKCU\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot\TurnOffWindowsCopilot` |
| Feedback | `HKCU\SOFTWARE\Microsoft\Siuf\Rules\NumberOfSIUFInPeriod` and `DoNotShowFeedbackNotifications` |
| Tailored experiences | `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy\TailoredExperiencesWithDiagnosticDataEnabled` |

Local data:

- Logs: `%LocalAppData%\PrivacyGuard\logs\`
- Database: `%LocalAppData%\PrivacyGuard\privacyguard.db`
- Preferences: `%LocalAppData%\PrivacyGuard\preferences.json`

## Project layout

```
PrivacyGuard/
├── PrivacyGuard.sln
├── README.md
├── preview/          Browser UI mock for Mac/Linux review
├── website/          Product site (landing, download, privacy)
└── src/PrivacyGuard/
    ├── Views/            Dashboard, Profiles, History, Settings
    ├── ViewModels/       CommunityToolkit.Mvvm
    ├── Models/           Snapshot, profiles, history types
    ├── Services/         Privacy, backup, history, elevation, dialogs
    ├── Helpers/          Registry, services, catalog, DI
    └── Assets/
```

## Tech stack

- .NET 8 (`net8.0-windows10.0.19041.0`, min OS 10.0.17763)
- WinUI 3 / Windows App SDK 1.7
- CommunityToolkit.Mvvm + SettingsControls
- Microsoft.Extensions.Hosting (DI)
- Serilog
- Microsoft.Data.Sqlite

## Next recommended steps

1. **Authenticode signing** — sign `PrivacyGuard.exe` and the Inno installer so SmartScreen stops warning, especially for elevated launches.
2. **Scheduled tasks** — enumerate and (optionally, with confirmation) disable known telemetry tasks such as `Microsoft Compatibility Appraiser` via the Task Scheduler API. Keep an allowlist.
3. **Group Policy / MDM overlap** — detect when a domain GPO already owns `AllowTelemetry` and show a read-only lock instead of failing on write.
4. **Packaged + unelevated helper** — keep the UI unpackaged-or-MSIX and isolate admin work in a tiny signed elevated COM/Win32 helper with a tightly scoped ACL.
5. **More Settings toggles** — location, clipboard cloud sync, Find My Device, inking & typing personalization, advertising in File Explorer.
6. **Automatic updates** — replace the placeholder “Check for updates” with GitHub Releases version checks.
7. **App icon** — replace the default executable icon before marketing screenshots.

## License

MIT. See [LICENSE](LICENSE). Use at your own risk; create a Windows System Restore point before aggressive experiments on a production PC.

## Releasing a public build

Do this from a machine with `git` and `gh`, then let GitHub Actions build on Windows:

1. **Create the GitHub repo** if you have not already. Edit `website/download.html` if the repo is not `Pakaweb/PrivacyGuard`.
   ```bash
   git init -b main
   git add .
   git commit -m "Initial public release of PrivacyGuard 0.1.0."
   gh repo create PrivacyGuard --public --source=. --remote=origin --push
   ```
2. **Enable GitHub Pages** — Settings → Pages → Build and deployment → Source: **GitHub Actions**. The `pages` workflow publishes `website/` to `https://<user>.github.io/PrivacyGuard/`.
3. **Smoke-test on Windows** before you tag: first-run dialog, Recommended profile, revert from History, Restore Default, Restart as administrator. On a Windows PC you can also run `./scripts/publish.ps1` (needs [Inno Setup 6](https://jrsoftware.org/isinfo.php)).
4. **Tag a release**
   ```bash
   git tag v0.1.0
   git push origin v0.1.0
   ```
   The `windows-release` workflow builds the x64 installer and opens a **draft** GitHub Release. Review it, then publish.
5. **Code-sign** `PrivacyGuard.exe` and the installer with an Authenticode certificate when you can. Until then, SmartScreen will warn (the download page already says so).

First launch shows a safety dialog (SKU limits, Maximum Privacy side effects, restore-point advice). Users must accept it before the dashboard is used.
