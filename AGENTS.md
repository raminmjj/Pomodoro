# AGENTS.md — Pomodoro App v3

## Overview

Minimal Pomodoro timer desktop app built with **.NET 10 + Avalonia 11.3 + SQLite**.
Compiles to native single-file binaries via **NativeAOT** for Windows, macOS, and Linux.

## Solution Structure

```
src/
├── Pomodoro.Domain/          # Pure entities, enums, interfaces (zero deps)
├── Pomodoro.Application/     # Engines, services, DTOs (depends on Domain only)
├── Pomodoro.Infrastructure/  # LiteDB, SharpHook, Audio, Autostart, Notifications
└── Pomodoro.App/             # Avalonia entry point, ViewModels, Views, DI
tests/
├── Pomodoro.Domain.Tests/         # xunit.v3 + AwesomeAssertions
├── Pomodoro.Application.Tests/
└── Pomodoro.Infrastructure.Tests/ # Integration tests with real LiteDB
```

## Architecture Rules (Onion Architecture)

- **Domain** has zero external dependencies. All interfaces live here.
- **Application** depends only on Domain. No UI, no DB, no platform code.
- **Infrastructure** implements Domain interfaces. Depends on Application + Domain.
- **App** is the composition root. Registers all DI services in `Program.cs`.
- Dependency flow: `App → Infrastructure → Application → Domain`

## Build & Test Commands

```bash
dotnet restore
dotnet build -c Release
dotnet test --collect:"XPlat Code Coverage"
dotnet run --project src/Pomodoro.App
```

### Publish (NativeAOT)

```bash
dotnet publish src/Pomodoro.App -c Release -r win-x64 /p:PublishAot=true
dotnet publish src/Pomodoro.App -c Release -r osx-arm64 /p:PublishAot=true
dotnet publish src/Pomodoro.App -c Release -r linux-x64 /p:PublishAot=true
```

## Tech Stack & Key Libraries

| Concern | Library | Notes |
|---------|---------|-------|
| UI | Avalonia 11.3.20 | Fluent theme, compiled bindings disabled by default |
| MVVM | CommunityToolkit.Mvvm 8.4.2 | Source generators, AOT-safe |
| Database | SQLite (via Microsoft.Data.Sqlite) | Embedded relational, AOT-friendly |
| Activity Tracking | SharpHook 7.1.3 | P/Invoke, AOT-friendly |
| Charts | LiveChartsCore 2.0.5 | SkiaSharp Avalonia bindings |
| Logging | Serilog 4.4.0 | File + Console sinks |
| DI | Microsoft.Extensions 10.0.11 | Explicit registration, no assembly scanning |
| Tests | xunit.v3 3.2.2 + AwesomeAssertions + NSubstitute | |

Package versions are centrally managed in `Directory.Packages.props`. Never add version attributes to individual `<PackageReference>` elements.

## Coding Conventions

- **Nullable**: enabled globally (`<Nullable>enable</Nullable>`)
- **Implicit usings**: enabled
- **LangVersion**: latest
- **Target framework**: net10.0
- All async methods must accept `CancellationToken`
- ViewModels inherit `BaseViewModel` (provides `IsBusy`, `ErrorMessage`, `RunSafeAsync`)
- DI registration is explicit in `ServiceCollectionExtensions.AddPomodoroInfrastructure()` and `Program.cs` — no reflection-based scanning
- The Pomodoro engine is a singleton; only one cycle runs at a time

## Platform-Specific Gotchas

- **Audio**: Pure P/Invoke implementation (`CrossPlatformSoundPlayer`). Uses `winmm.dll` on Windows, `afplay` on macOS, `paplay`/`aplay` on Linux. No NAudio.
- **Autostart**: Platform-specific implementations selected via `AutoStartServiceFactory` (Registry on Windows, LaunchAgent on macOS, systemd --user on Linux).
- **Activity tracking**: SharpHook uses P/Invoke. `NullActivityTracker` exists as a fallback.
- **Database**: SQLite via `Microsoft.Data.Sqlite`. Schema managed by `SqliteDbContext` with idempotent migrations.
- **Data directory**: `%LOCALAPPDATA%/Pomodoro/` on Windows, equivalent on other platforms. DB file: `pomodoro.db`.
- **Sounds**: Bundled WAV files in `Assets/Sounds/` (bell.wav, chime.wav, digital.wav), copied to output.
- **Notifications**: `Avalonia.Labs.Notifications` for OS-level toasts. `DesktopNotificationSink` handles click-to-restore via `NotificationCompleted` event (`e.IsActivated`). Works even when app is minimized to tray.

## Avalonia-Specific Notes

- Compiled bindings are **disabled** by default (`AvaloniaUseCompiledBindingsByDefault=false`)
- Use `Avalonia.Diagnostics` only in Debug configuration
- Views use code-behind + AXAML pattern (not pure MVVM binding-only)
- Navigation is handled via `INavigationService` registered in App layer
- Timer ticks use `DispatcherTimerTickScheduler` to marshal to UI thread

## Documentation

- Full architecture and feature details: `README.md`
- CI/CD pipeline: `.github/workflows/ci.yml`
- MSBuild props: `Directory.Build.props`, `Directory.Packages.props`
