# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**语言偏好：使用中文进行注释。**

## Project Overview

A SolidWorks desktop companion app (WPF toolbar). The desktop app communicates with a SolidWorks add-in via local HTTP/WebSocket loopback — no direct COM references at compile time.

## Build & Test Commands

```bash
# Restore
dotnet restore ExternalProgram.sln -r win-x64

# Build all
dotnet build ExternalProgram.sln -c Release

# Build SwAddin first (required before publishing main app)
dotnet build ExternalProgram.SwAddin/ExternalProgram.SwAddin.csproj -c Release

# Publish single-file exe (framework-dependent, must build SwAddin first)
dotnet publish ExternalProgram.csproj -c Release -r win-x64 --self-contained false -o publish/ExternalProgram

# Package for release (includes .NET runtime by default)
python scripts/package.py
python scripts/package.py --skip-runtime  # without runtime installer
```

SDK: .NET 9.0 (`global.json`). All projects target `x64` only (SolidWorks is 64-bit).

## Architecture

Two projects, two distinct deployables:

| Project | Target | Role |
|---------|--------|------|
| `ExternalProgram.csproj` | `net9.0-windows` WinExe | Desktop toolbar app — always-on-top WPF window, tray icon, Ctrl+F1 hotkey |
| `ExternalProgram.SwAddin` | `net48` library | COM-registered SolidWorks add-in — HTTP+WebSocket server with ~40 command handlers, split by feature into `AddinHttpServer.Commands.*.cs` files |

**Integration pattern — loopback HTTP bridge:**

```
ExternalProgram (Form1)                     SolidWorks process
    SwAddinClient                           SwAddin (COM-loaded)
      |-- HTTP /health            ------->    |-- AddinHttpServer (port 32128+)
      |-- HTTP POST /command      ------->    |
      |-- WebSocket /events       <-------    |
```

No project references between the desktop apps and the add-in DLLs. They communicate over `127.0.0.1` only. The SwAddin DLLs are copied to output via MSBuild targets (`CopySwAddinToOutput`, `CopySwAddinToPublishDir`).

**Thread marshaling:** SolidWorks COM APIs are single-threaded. All command execution in `AddinHttpServer` marshals to the SolidWorks main thread via `Control.BeginInvoke()`.

## Key Files

- `Program.cs` — desktop app entry point, WPF-UI theme loading
- `Form1.xaml(.cs)` — main toolbar, tray icon, button handlers, `SwAddinClient` connection
- `SwAddinClient.cs` — HTTP+WebSocket client, auto-discovers SolidWorks instances (foreground window heuristic, port scan 32128-32187)
- `ExternalProgram.SwAddin/SwAddin.cs` — COM add-in entry point, COM registration, starts HTTP servers directly
- `ExternalProgram.SwAddin/AddinHttpServer.cs` — HTTP+WebSocket server, JSON command dispatch
- `ExternalProgram.SwAddin/AddinHttpServer.Commands.cs` — command registration/dispatch table; individual handlers live in the `AddinHttpServer.Commands.*.cs` partial files (e.g. `.Rename.cs`, `.Properties.cs`, `.CodingCleanup.cs`, `.SortComponents.cs`)
- `ToolbarButtonLayout.cs` — toolbar button definitions, groups, per-document-type visibility
- Desktop UI windows (main project root): `CodingCleanupWindow`, `RenameWindow`, `SettingsWindow`, `PropertyOverlayWindow` (+ `PropertyOverlayDefaults.cs`)

## Custom MSBuild Targets

The main `ExternalProgram.csproj` has three custom targets:
- `BuildSwAddin` (before `Build;Publish`) — conditionally builds the SwAddin project first, gated by `$(BuildSwAddinDuringMainBuild)` (defaults to `false`, so by default you must build SwAddin manually — see Build & Test Commands)
- `CopySwAddinToOutput` (after `Build`) — copies SwAddin COM DLLs from `ExternalProgram.SwAddin\bin\$(Configuration)\net48\` to the build output (`$(TargetDir)`)
- `CopySwAddinToPublishDir` (after `Publish`) — copies same DLLs to `$(PublishDir)SwAddin\`

`ExternalProgram.SwAddin.csproj` is a standard SDK project: it implicitly compiles the `.cs` files directly under
`ExternalProgram.SwAddin/`.

The main `ExternalProgram.csproj` excludes `ExternalProgram.SwAddin\**\*.cs` from
its own compilation, so the root-level UI files are the desktop app's sources.

> **Note:** The `ReadBom` project has been split out into its own repository at `../readbomsl`.
