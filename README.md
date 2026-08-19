# Build

[![CI/CD](https://github.com/raminmjj/Pomodoro/actions/workflows/ci.yml/badge.svg)](https://github.com/raminmjj/Pomodoro/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A minimal, focused Pomodoro timer built with **.NET 10 + Avalonia 12 + SQLite**.
Compiles to native single-file binaries for **Windows**, **macOS**, and **Linux** via
**NativeAOT** (no runtime dependency, fast startup, small binary).

## Features

- Minimal, distraction-free UI
- Task management (CRUD)
- Configurable Pomodoro/Break durations
- Native system notifications + cross-platform alarm sound (zero external deps)
- Windows notifications via WinRT (with app icon in toasts + settings)
- Linux notifications via classic D-Bus `org.freedesktop.Notifications` (works on GNOME 3.36+, KDE, XFCE, etc.)
- Clicking a notification restores the window (even when minimized to tray)
- Minimize-to-tray: minimize button hides to the system tray with an Open/Exit menu
- Activity tracking during breaks (keyboard + mouse) via SharpHook
- Idle/over-activity alerts with cooldown
- Beautiful daily report with LiveChartsCore (KPI cards, pie chart, hourly activity)
- Auto-start with system login (Registry / LaunchAgent / systemd --user)
- Single-file AOT-compiled binaries per platform
- Portable storage: DB lives next to the executable; falls back to per-user data dir when the app root is not writable

## Tech Stack

| Concern            | Library                          | Notes                                        |
| ------------------ | -------------------------------- | -------------------------------------------- |
| UI Framework       | Avalonia 12.1.1                  | Latest stable                                |
| MVVM               | CommunityToolkit.Mvvm 8.4.2      | Source-generator based, AOT-safe              |
| Database           | Microsoft.Data.Sqlite 10.0.11    | Embedded SQLite, AOT-safe (replaced LiteDB)   |
| Activity Tracking  | SharpHook 7.1.3                  | P/Invoke wrapper, AOT-friendly                |
| Charts             | LiveChartsCore 2.1.0-dev-798     | SkiaSharp-backed, Avalonia 12 support         |
| Notifications      | Avalonia.Labs.Notifications 12.0.2 | WinRT toasts (Windows), D-Bus/Portal (Linux) |
| D-Bus Protocol     | Tmds.DBus.Protocol 0.94.1        | Classic `org.freedesktop.Notifications` on Linux |
| Audio Playback     | Pure P/Invoke (no external deps) | winmm.dll / afplay / paplay-aplay             |
| Logging            | Serilog 4.4.0                    | File + console sinks                          |
| DI / Host          | Microsoft.Extensions 10.0.11     | Explicit registration (no assembly scan)      |

> **Note on Audio**: Original plan suggested NAudio for Windows. NAudio is
> Windows-only, so it has been replaced with a pure P/Invoke implementation that
> uses `winmm.dll` on Windows, `afplay` on macOS, and `paplay`/`aplay` on Linux —
> **zero external audio dependencies**.

| Test Framework     | xunit.v3 3.2.2 + AwesomeAssertions | OSS test stack                             |

## Solution Layout

```
Pomodoro/
├── Directory.Build.props              # Common MSBuild props
├── Directory.Packages.props           # Central package management
├── Pomodoro.slnx
├── src/
│   ├── Pomodoro.Domain/               # Entities, Enums, Interfaces
│   │   ├── Entities/
│   │   ├── Enums/
│   │   ├── Events/
│   │   └── Interfaces/
│   ├── Pomodoro.Application/          # Engines, Services, DTOs
│   │   ├── Engines/
│   │   ├── Services/
│   │   └── DTOs/
│   ├── Pomodoro.Infrastructure/       # SQLite, SharpHook, Audio, Autostart
│   │   ├── Persistence/
│   │   ├── Hooks/
│   │   ├── Notifications/
│   │   ├── Audio/
│   │   ├── Autostart/
│   │   └── Logging/
│   └── Pomodoro.App/                  # Avalonia entry point
│       ├── ViewModels/
│       ├── Views/
│       ├── Styles/
│       ├── Services/
│       ├── Assets/Sounds/             # bell.wav, chime.wav, digital.wav
│       └── Properties/PublishProfiles/
└── tests/
    ├── Pomodoro.Domain.Tests/         # 6 tests
    ├── Pomodoro.Application.Tests/    # 24 tests
    ├── Pomodoro.Infrastructure.Tests/ # 8 integration tests (real SQLite)
    └── Pomodoro.App.Tests/            # 9 tests (E2E + headless UI)
```

## Build & Run

```bash
# Restore
dotnet restore

# Run (debug)
dotnet run --project src/Pomodoro.App

# Run tests
dotnet test
```

## Publish native AOT (per platform)

```bash
# Windows x64
dotnet publish src/Pomodoro.App -c Release -r win-x64 /p:PublishAot=true

# macOS arm64
dotnet publish src/Pomodoro.App -c Release -r osx-arm64 /p:PublishAot=true

# Linux x64
dotnet publish src/Pomodoro.App -c Release -r linux-x64 /p:PublishAot=true
```

Outputs land in `bin/Release/publish/<RID>/` as a single self-contained
native binary.

## Architecture

The solution follows an **Onion Architecture** with strict dependency rules:

```
App →  Infrastructure  →  Application  →  Domain
```

- **Domain**: pure entities, enums, interfaces. Zero external dependencies.
- **Application**: engines, services, DTOs. Depends only on Domain.
- **Infrastructure**: SQLite (Microsoft.Data.Sqlite), SharpHook, audio, autostart. Implements Domain interfaces. Depends on Application + Domain.
- **App**: Avalonia views, viewmodels, DI registration. Composition root — references all layers.

All async work is `CancellationToken`-aware. The Pomodoro state machine
is a singleton — only one cycle can run at a time.

## Tests

- **Domain.Tests** (6): Entity defaults, IsActive logic.
- **Application.Tests** (24): Engine state transitions, TaskService CRUD,
  SettingsService defaults/persistence, ActivityAlertEvaluator cooldown.
- **Infrastructure.Tests** (8): SQLite round-trip for all entity types,
  including unique-index enforcement.
- **App.Tests** (9): E2E Avalonia headless tests — UI state, navigation,
  tick scheduler binding, settings persistence.

## License

MIT
