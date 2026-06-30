# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A SolidWorks desktop companion app (WPF toolbar) + BOM reader. The desktop app communicates with a SolidWorks add-in via local HTTP/WebSocket loopback — no direct COM references at compile time.

## Build & Test Commands

```bash
# Restore
dotnet restore ExternalProgram.sln -r win-x64

# Build all
dotnet build ExternalProgram.sln -c Release

# Build SwAddin first (required before publishing main app)
dotnet build ExternalProgram.SwAddin/ExternalProgram.SwAddin.csproj -c Release
dotnet build ExternalProgram.SwAddin.Runtime/ExternalProgram.SwAddin.Runtime.csproj -c Release

# Publish single-file exe (framework-dependent, must build SwAddin first)
dotnet publish ExternalProgram.csproj -c Release -r win-x64 --self-contained false -o publish/ExternalProgram
dotnet publish ReadBom/ReadBom.csproj -c Release -r win-x64 --self-contained false -o publish/ReadBom

# Run tests
dotnet test ExternalProgram.Tests/ExternalProgram.Tests.csproj

# Package for release (includes .NET runtime by default)
python scripts/package.py
python scripts/package.py --skip-runtime  # without runtime installer
```

SDK: .NET 9.0 (`global.json`). All projects target `x64` only (SolidWorks is 64-bit).

## Architecture

Five projects, three distinct deployables:

| Project | Target | Role |
|---------|--------|------|
| `ExternalProgram.csproj` | `net9.0-windows` WinExe | Desktop toolbar app — always-on-top WPF window, tray icon, Ctrl+F1 hotkey |
| `ExternalProgram.SwAddin` | `net48` library | COM-registered SolidWorks add-in shell — manages a child AppDomain |
| `ExternalProgram.SwAddin.Runtime` | `net48` library | Add-in engine loaded in child AppDomain — HTTP+WebSocket server with ~40 SolidWorks command handlers |
| `ReadBom` | `net9.0-windows` WinExe | Standalone WPF BOM viewer/editor, can work offline via Document Manager |
| `ExternalProgram.Tests` | `net9.0-windows` library | xUnit tests for `DrawingBatchScanner` and `PaperFormat` |

**Integration pattern — loopback HTTP bridge:**

```
ExternalProgram (Form1)                     SolidWorks process
    SwAddinClient                           SwAddin shell (COM-loaded)
      |-- HTTP /health            ------->    |-- starts child AppDomain
      |-- HTTP POST /command      ------->    |   SwAddinRuntime
      |-- WebSocket /events       <-------    |     AddinHttpServer (port 32128+)
                                               |     ReadBom.AddinHttpServer (port 32127+)
ReadBom (MainWindow)
    SolidWorksAddinClient
      |-- HTTP /command           ------->  (same child AppDomain, port 32127+)
```

No project references between the desktop apps and the add-in DLLs. They communicate over `127.0.0.1` only. The SwAddin shell copies DLLs to output via MSBuild targets (`CopySwAddinToOutput`, `CopySwAddinToPublishDir`).

**Hot-reload:** The SwAddin shell watches the `Runtime/current/` directory via `FileSystemWatcher`. When the runtime DLL changes, it tears down and recreates the child AppDomain — no SolidWorks restart needed.

**Thread marshaling:** SolidWorks COM APIs are single-threaded. All command execution in `AddinHttpServer` marshals to the SolidWorks main thread via `Control.BeginInvoke()`.

## Key Files

- `Program.cs` — desktop app entry point, WPF-UI theme loading
- `Form1.xaml(.cs)` — main toolbar, tray icon, button handlers, `SwAddinClient` connection
- `SwAddinClient.cs` — HTTP+WebSocket client, auto-discovers SolidWorks instances (foreground window heuristic, port scan 32128-32187)
- `ExternalProgram.SwAddin/SwAddin.cs` — COM add-in shell, AppDomain management, COM registration (`[ComRegisterFunction]`)
- `ExternalProgram.SwAddin.Runtime/SwAddinRuntime.cs` — `MarshalByRefObject` entry, starts both HTTP servers
- `ExternalProgram.SwAddin.Runtime/AddinHttpServer.cs` — HTTP+WebSocket server, JSON command dispatch
- `ExternalProgram.SwAddin.Runtime/AddinHttpServer.Commands.cs` — ~40 command handler methods (drawing automation, rename, properties, coding cleanup)
- `ExternalProgram.SwAddin.Runtime/ReadBomMerged/` — second HTTP server for BOM-specific commands, merged into the same runtime assembly
- `ReadBom/MainWindow.xaml(.cs)` — BOM DataGrid viewer with export, find/replace, column fill
- `ReadBom/OfflineSolidWorksReader.cs` — BOM reading via SolidWorks Document Manager (no SolidWorks needed)
- `ToolbarButtonLayout.cs` — toolbar button definitions, groups, per-document-type visibility

## Custom MSBuild Targets

The main `ExternalProgram.csproj` has three custom targets:
- `BuildSwAddin` — conditionally builds the SwAddin project before main build (gated by `$(BuildSwAddinDuringMainBuild)`)
- `CopySwAddinToOutput` (after `Build`) — copies SwAddin COM DLLs from `SwAddin\bin\Release\net48\` to the build output
- `CopySwAddinToPublishDir` (after `Publish`) — copies same DLLs to `$(PublishDir)SwAddin\`

The `SwAddin.Runtime.csproj` has `CopyRuntimeToSwAddinPackage` (after `Build`) — copies all runtime DLLs + SolidWorks WPF dependencies to `SwAddin\bin\{Config}\net48\Runtime\current\`.