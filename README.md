# SuperDelete

### About

SuperDelete deletes files and folders with **very long paths** (longer than the classic Windows
`MAX_PATH` of 260 characters — up to 32 767 characters). It works by using extended-length paths
(`\\?\`) together with the Unicode versions of the Win32 functions for enumerating and deleting files.
It can also **bypass ACL checks** when deleting, provided the user has administrative rights on the
drive — useful when a disk has been moved from another machine or Windows installation.

More on the underlying mechanism: MSDN —
[Naming Files, Paths, and Namespaces](https://learn.microsoft.com/windows/win32/fileio/naming-a-file),
section *Maximum Path Length Limitation*.

SuperDelete now ships in **two forms that share one deletion engine**:

- **`SuperDelete.exe`** — the original command-line tool (same flags, same behavior).
- **SuperDelete (desktop app)** — a modern WPF interface so non-technical users can delete a long
  path safely, without using a terminal.

---

### Project layout

| Project | Type | Purpose |
|---|---|---|
| `src/SuperDelete.Core` | class library (`net8.0-windows`) | The shared deletion engine: long-path delete, preview/analysis, result models, service interfaces. Single source of truth. |
| `src/SuperDelete.Cli` | console app (`net8.0-windows`) | Thin CLI over the engine. Builds to **`SuperDelete.exe`**. |
| `src/SuperDelete.App` | WPF app (`net8.0-windows`) | The desktop UI over the same engine. |

The engine is exposed through interfaces (`IDeletionService`, `IPathAnalyzer`) and reports progress via
`IProgress<DeletionProgress>`, so it carries no console/UI dependency. See
[`docs/NOTES.md`](docs/NOTES.md) for design decisions and future improvements.

---

### Build

Requires the **.NET 8 SDK** (`dotnet --version` ≥ 8.0). From the repository root:

```
dotnet build SuperDelete.sln -c Release
```

Build a single project, e.g. just the CLI:

```
dotnet build src/SuperDelete.Cli -c Release
```

> The project is Windows-only: it depends on Win32 APIs (`kernel32`, `advapi32`, `ntdll`) and WPF.

---

### Run the CLI

The CLI is unchanged from the original tool. It takes a single file or folder path plus optional
switches.

#### With confirmation
```
SuperDelete.exe <full path to file or folder>
```

#### Silent mode
Suppresses the confirmation prompt (useful for automation). Switch: `-s` or `--silentMode`.
```
SuperDelete.exe --silentMode <path>
```

#### Bypass ACLs
If the user has administrative rights on the drive, removes the item even without ACL permission.
Run from an **elevated** (Administrator) prompt.
```
SuperDelete.exe --bypassAcl <path>
```

#### Print stack trace
On error, prints the full call stack instead of just the message (for debugging).
```
SuperDelete.exe --printStackTrace <path>
```

Switches can be combined, and the order does not matter. To run via the SDK during development:

```
dotnet run --project src/SuperDelete.Cli -- -s "C:\some\very\long\path"
```

---

### Run the desktop app

```
dotnet run --project src/SuperDelete.App
```

(or launch `src/SuperDelete.App/bin/<config>/net8.0-windows/SuperDelete.App.exe` after building).

To use **Bypass ACL** from the UI, start the app **as administrator** — the option is available either
way, and an indicator reminds you when it is enabled.

#### Screens

The window is a single, sober utility screen organized top-to-bottom:

1. **Header** — app name, one-line description, and a **Dark mode** toggle (switches theme live).
2. **File or folder to delete** — a path box plus **Browse file…** / **Browse folder…**. You can also
   **paste** a path or **drag-and-drop** a file/folder anywhere onto the window. A **Recent** dropdown
   lists paths used this session (in memory only).
3. **Actions** — **Analyze / Preview**, **Delete** (danger-styled), and **Cancel** (shown while busy).
   While running, an indeterminate progress bar shows the current item and a live `files · folders`
   count.
4. **Summary** (after Analyze) — type (file/folder), existence, **path length** with a badge when it
   exceeds 260 characters, item counts, an amber note for access/ACL or reparse-point concerns, and a
   red **warning** before recursive deletion (“This will permanently delete N items…”).
5. **Advanced options** (collapsible) — **Preview only** (dry run, deletes nothing), **Bypass ACL**
   (with a visible "enabled" indicator), **Show diagnostic details**, and a short explanation of the
   Windows long-path limitation.
6. **Activity log** — timestamped, real-time log with a **Clear** button and a collapsible **Technical
   details** panel (stack traces) shown when something fails.
7. **Status bar** — a colored final status: green = success, amber = cancelled/partial, red = failure.

Safety behaviors: a real deletion always asks for an **explicit confirmation** that names the target and
item count; **Preview only** never changes anything; the UI stays responsive during long deletes and the
operation can be **cancelled** mid-run.

---

### Downloads

The original command-line releases remain available on the upstream
[Releases](https://github.com/marceln/SuperDelete/releases) page.

### License

Apache License 2.0 — see [LICENSE](LICENSE).
