# Build
[![CI/CD](https://github.com/pomodoro/pomodoro-app/actions/workflows/ci.yml/badge.svg)](https://github.com/pomodoro/pomodoro-app/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A minimal, focused Pomodoro timer built with **.NET 10 + Avalonia 11.3 + SQLite**.
Compiles to native single-file binaries for **Windows**, **macOS**, and **Linux** via
**NativeAOT** (no runtime dependency, fast startup, small binary).

## Features

- Minimal, distraction-free UI
- Task management (CRUD)
- Configurable Pomodoro/Break durations
- Native system notifications + cross-platform alarm sound (zero external deps)
- Clicking a notification restores the window (even when minimized to tray)
- Minimize-to-tray: minimize button hides to the system tray with an Open/Exit menu
- Activity tracking during breaks (keyboard + mouse) via SharpHook
- Idle/over-activity alerts with cooldown
- Beautiful daily report with LiveChartsCore (KPI cards, pie chart, hourly activity)
- Auto-start with system login (Registry / LaunchAgent / systemd --user)
- Single-file AOT-compiled binaries per platform

## Tech Stack

| Concern            | Library                          | Notes                                  |
| ------------------ | -------------------------------- | -------------------------------------- |
| UI Framework       | Avalonia 11.3.20                 | Latest stable                         |
| MVVM               | CommunityToolkit.Mvvm 8.4.2      | Source-generator based, AOT-safe       |
| Database           | Microsoft.Data.Sqlite 10.0.11    | Embedded SQLite, AOT-safe (replaced LiteDB) |
| Activity Tracking  | SharpHook 7.1.3                  | P/Invoke wrapper, AOT-friendly         |
| Charts             | LiveChartsCore 2.0.5             | SkiaSharp-backed, Avalonia bindings    |
| Notifications      | Avalonia.Labs.Notifications 11.3.1 | Cross-platform native notifications  |
| Audio Playback     | Pure P/Invoke (no external deps) | winmm.dll / afplay / paplay-aplay     |
| Logging            | Serilog 4.4.0                    | File + console sinks                   |
| DI / Host          | Microsoft.Extensions 10.0.11     | Explicit registration (no assembly scan) |

> **Note on Audio**: Original plan suggested NAudio for Windows. NAudio is
> Windows-only, so it has been replaced with a pure P/Invoke implementation that
> uses `winmm.dll` on Windows, `afplay` on macOS, and `paplay`/`aplay` on Linux —
> **zero external audio dependencies**.

| Test Framework     | xunit.v3 3.2.2 + AwesomeAssertions | OSS test stack                       |

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
    ├── Pomodoro.Application.Tests/    # 23 tests
    └── Pomodoro.Infrastructure.Tests/ # 5 integration tests (real SQLite)
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
App  →  Application  →  Domain
                    ↑
          Infrastructure  →  Application  →  Domain
```

- **Domain**: pure entities, enums, interfaces. Zero external dependencies.
- **Application**: engines, services, DTOs. Depends only on Domain.
- **Infrastructure**: SQLite (Microsoft.Data.Sqlite), SharpHook, audio, autostart. Implements Domain interfaces.
- **App**: Avalonia views, viewmodels, DI registration.

All async work is `CancellationToken`-aware. The Pomodoro state machine
is a singleton — only one cycle can run at a time.

## Tests

- **Domain.Tests** (6): Entity defaults, IsActive logic.
- **Application.Tests** (23): Engine state transitions, TaskService CRUD,
  SettingsService defaults/persistence, ActivityAlertEvaluator cooldown.
- **Infrastructure.Tests** (5): SQLite round-trip for all 5 entity types,
  including unique-index enforcement.

## License

MIT
