# Technical notes

This document records the decisions behind the v2 restructuring (shared engine + CLI + WPF UI) and the
known trade-offs and future work.

## Goals

1. Keep the proven long-path deletion logic as the **single source of truth**.
2. Make it reusable by both a CLI and a UI.
3. Add a trustworthy desktop app for non-technical users.
4. Don't break the existing CLI behavior.

## Architecture

Three SDK-style projects targeting `net8.0-windows`:

- **`SuperDelete.Core`** — the engine and its contracts.
- **`SuperDelete.Cli`** — a thin console front-end (builds to `SuperDelete.exe`).
- **`SuperDelete.App`** — a WPF front-end.

Both front-ends depend only on `SuperDelete.Core`; they never touch the Win32 layer directly.

### Why the engine had to be refactored

The original `FileDeleter` could not be reused as-is:

- it was `static`, so it had no per-operation state;
- progress was a `Console`-writing **singleton** (`ProgressTracker`) — fine for a CLI, impossible for a UI;
- there was no **cancellation** and no **preview/dry-run**;
- everything was `internal`.

### What changed (and what didn't)

- The **recursion and every Win32 call are preserved verbatim** (`FindFirstFileW`/`FindNextFileW`,
  `DeleteFileW`, `RemoveDirectoryW`, backup-semantics delete via `CreateFile` + `NtSetInformationFile`,
  reparse-point handling, read-only attribute clearing, privilege enabling for Bypass ACL). The risky,
  battle-tested code was **encapsulated, not rewritten**.
- `FileDeleter` → `DeletionService : IDeletionService`: instance-based, reports through
  `IProgress<DeletionProgress>`, accepts a `CancellationToken`, and supports `DeletionOptions.PreviewOnly`
  (it walks and counts but performs no disk changes, and never demands admin privileges).
- Instead of throwing, an operation returns a structured `DeletionResult` with a `DeletionStatus`
  (`Success` / `Partial` / `Failed` / `Cancelled` / `PreviewCompleted`). `Partial` is reported when some
  items were already removed before an error — giving the UI an honest "partial" outcome. The engine is
  still **fail-fast** (stops at the first error), exactly like the original.
- New `PathAnalyzer : IPathAnalyzer` powers the UI Preview/summary. It reuses the same extended-length
  enumeration, so counts are correct for >260-char paths, and it is best-effort: missing paths and
  access-denied are surfaced on `PathAnalysis`, never thrown.
- `ProgressTracker` and `Utils.PathShortener` were folded into `PathUtils` and the front-ends. The CLI
  reproduces the original on-screen output (`Deleting …` / `Done. Deleted N files and M folders in …`).
- `Resources.resx` (CLI strings) → plain `Messages` constants, so the CLI builds with the `dotnet` CLI
  without the VS-only resx code generator. The user-visible text is unchanged.

### UI framework choice: WPF

Chosen over WinForms / WinUI 3 / Avalonia because it is mature, first-class on .NET 8, and its XAML
data-binding makes the safety-focused UX (live log, async progress, theming, confirmation) straightforward
with **no extra NuGet dependencies**. .NET 8 provides a native `OpenFolderDialog`, so no WinForms interop
is needed. The app is plain MVVM (`ViewModelBase`, `RelayCommand`/`AsyncRelayCommand`) — no framework.

### UI responsiveness detail

`Progress<T>` marshals every report onto the UI thread, which would flood the dispatcher on a large tree.
Instead the engine reports through a synchronous `DelegateProgress<T>` that only updates `volatile`
counters on the worker thread; a 100 ms `DispatcherTimer` samples them into bound properties. The UI stays
smooth regardless of delete throughput, and the operation runs on a background thread via
`IDeletionService.DeleteAsync` (a `Task.Run` wrapper).

### Theming

`App.xaml` merges a theme dictionary at index 0; `App.ApplyTheme(bool)` swaps `Light.xaml`/`Dark.xaml` at
runtime. Both dictionaries define the **same brush keys**, and all controls use `DynamicResource`, so dark
mode is a one-line dictionary swap and new themes are easy to add.

## Compatibility impact

- **CLI behavior preserved**: same switches (`-s`/`--silentMode`, `--bypassAcl`, `--printStackTrace`),
  same confirmation prompt and console output, same `SuperDelete.exe` assembly name.
- **Dropped**: the legacy multi-targeted `.NET Framework` 3.5/4.0/4.5/4.6 project files. The tool now
  targets `net8.0-windows` and requires the .NET 8 runtime/SDK. This is the deliberate
  "modern .NET" trade-off chosen for maintainability.

## Future improvements

- A unit-test project for `SuperDelete.Core` (e.g. xUnit) exercising deep trees, read-only files,
  reparse points, preview counts, and cancellation against temp fixtures.
- Determinate progress by pre-counting (the analyzer already produces a total) — show a real percentage.
- Optional "send to Recycle Bin" mode for non-destructive deletes.
- A multi-path queue / batch deletion in the UI.
- Persist recent paths and settings (e.g. last theme) between sessions.
- Auto-elevation prompt when **Bypass ACL** is selected but the process is not elevated.
- Re-introduce localization via resx in the Core/front-ends.
- Signed, single-file `dotnet publish` artifacts for both the CLI and the app, wired into CI.
