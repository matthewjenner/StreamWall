# CLAUDE.md

Guidance for working in this repo. `Docs/` holds the design, the architecture,
and the work plan (living tracker + decisions log). This file is the quick
orientation - read it first, and read **Privacy** before touching anything.

## What this is

StreamWall - a Windows app that shows many signed-in web video streams at once
as an always-on video wall. One or more borderless windows ("walls") each hold
a grid of cells; each cell is an embedded WebView2 trimmed to a single
`<video>`. It replaces opening ~12 browser tabs on a site that needs a login
and starting Picture-in-Picture in each one.

It is **.NET 10 + Avalonia 12**, following Klakr's layout and the standards of
Updatr and RingView (the newest local apps), released with Velopack from this
**public** repo. `Docs/workplan.md` has the phases and decisions.

Status: **M3 (Browser and walls) done.** The control panel (Walls / Browser /
Streams / Settings), 1-3 wall windows, Send to wall with the picker, and the
demo site work. Next: M4 wall polish, M5 resilience (see `Docs/workplan.md`).
The M1 spike lives in `Spikes/StreamWall.WebViewSpike`.

## Privacy (non-negotiable)

The site, its URLs, hosts, account names and credentials are **sensitive**.
They must never appear in the repo, git history, logs, diagnostics, error
text, or any AI transcript. The repo is public, so this also means "never
published".

- **Never ask for, guess, print or echo real URLs, domains or credentials.**
  Code, tests, docs, samples and commit messages use placeholders under
  `example.invalid` (e.g. `https://stream.example.invalid/view/1`).
- **Nothing site-specific is hard-coded.** Every URL and selector
  is captured from the Browser tab or entered by the user in the app at runtime.
- **Never open, read or parse** the encrypted config (`%APPDATA%\StreamWall\App\config.dat`)
  or the WebView2 profile (`%LOCALAPPDATA%\StreamWall\WebView`) from tools.
  The log files are safe to read: they are redacted and pseudonymized by design.
- **Sites appear only as pseudonyms** (`site-7f3a9c`) and streams as cell
  index + stream pseudonym (`cell 3 stream-1b2c4d`). User-given labels are
  never logged (users can type anything into a label).
- **Review the UI with `--demo` screenshots, never the real wall.** Never
  capture the real app's windows (they show the user's streams).
- Diagnostics go through typed `DiagLog` methods, which redact; no free-form
  log strings elsewhere. A URL-shaped string reaching a log is a bug.

## Commands

```bash
dotnet build StreamWall.slnx
dotnet test --solution StreamWall.slnx    # MTP runner (global.json); conventions, unit, headless UI
dotnet run --project Src/StreamWall.App   # the real app, your config
dotnet run --project Src/StreamWall.App -- --demo   # demo site, memory-only config/log, demo-only profile
dotnet run --project Src/StreamWall.App -- --demo --screenshot <folder>  # Send to wall, PNGs, quit
./Scripts/run.sh [Debug|Release] [app args]     # clean, build, run
./Scripts/bump-version.sh [Major|Minor|Patch]   # edits Directory.Build.props only
./Spikes/gen-media.sh                           # the spike's synthetic clip (needs ffmpeg)
```

Coming later: `./Scripts/pack.sh` (M6). Headless UI tests cannot run WebView2,
so anything involving cells or the Browser is checked with `--demo --screenshot`
and by reading the PNGs (demo pages only, never the real app).

Enable the pre-commit scan once per clone: `git config core.hooksPath .githooks`
(runs gitleaks on staged changes: secrets, plus any URL whose host is not on the
`.gitleaks.toml` allowlist; CI scans the full history).

## Layout

```
Src/StreamWall.Core/       Pure logic. Now: Config (AppConfig, WallConfig, WallLayout, ConfigRules,
                           ConfigDocument, ConfigRepository + IConfigStore), Diagnostics (DiagLog,
                           LogRedactor, Pseudonymizer), Presentation (UrlMask, WallMap). Later:
                           presets, CellWatchdog, CellDisplay.
                           net10.0, no packages, no P/Invoke (CorePurityConventionTests).
Src/StreamWall.Platform/   Windows services. Now: DpapiConfigStore (config.dat, atomic, sets an
                           unreadable file aside), FileDiagSink, AppPaths. Later: profile
                           hygiene, SingleInstance, VelopackUpdater.
Src/StreamWall.App/        Avalonia app -> StreamWall.exe: AppHost (composition root, real or
                           --demo), WallManager + WallWindow + CellView (1-3 walls), the control
                           panel (MainWindow tabs, BrowserView, PickerWindow, WallMapView),
                           Web/ (WebEnvironment, injected Scripts/, the DemoSite), Screenshots.
                           Later: tray, single instance, updater.
Tests/StreamWall.Core.Tests/     Conventions (ASCII, US English, Core purity, URL allowlist),
                                 redaction, pseudonyms, config rules/document/repository.
Tests/StreamWall.Platform.Tests/ Real DPAPI round-trip and quarantine, log file, paths (temp folders).
Tests/StreamWall.App.Tests/      Headless UI tests on the demo host (UiHarness; not Avalonia.Headless.XUnit).
Spikes/                    Throwaway proofs, outside the solution and central packages.
Scripts/                   run, bump-version (pack, gen-icons in M6).
Directory.Build.props      Version + shared Windows TFM.
Directory.Build.targets    Drops WebView2's WPF/WinForms references (MSB3277).
Directory.Packages.props   Central package versions (check nuget.org before adding/bumping).
```

## Conventions

- **ASCII and US English** everywhere authored, enforced by
  `AsciiConventionTests` / `UsEnglishConventionTests`. Build non-ASCII data
  (the masking dot, status dots) from char codes.
- **No real hosts in the tree**, enforced by gitleaks' `non-placeholder-url`
  rule (pre-commit and CI) and `UrlAllowlistConventionTests`, which reads the
  same rule and allowlist from `.gitleaks.toml` (one source of truth): any URL
  whose host is not on the allowlist (`example.invalid`, `*.example`, and
  named build/docs hosts) fails. `gitleaks dir` also scans ignored build output
  (`bin/`, `obj/`, the generated clip); the git scans that matter do not.
- **Redaction order matters.** `LogRedactor` replaces URLs, then registered
  terms, then token shapes (JWT, Bearer, cookie headers) before generic
  `key=value`; the reverse leaks `Authorization: Bearer <token>`. Add a test
  case for any new shape.
- **Update `Docs/` in the same change** as the code it describes.
- **Core is pure and clock-free.** State machines take an injected
  `TimeProvider`; tests drive them with `FakeTimeProvider`.
- **WebView2 is UI-thread bound.** Every web view call happens on the Avalonia
  UI thread and is awaited; no `.Result`, `.Wait()` or `Thread.Sleep` on any
  path the UI waits on.
- **MVVM:** CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`),
  compiled bindings, ViewModels never reference Views; visual rules live in
  Core (`CellDisplay`), view models stay thin.
- **Airspace:** native web views draw above Avalonia content, so nothing
  renders over a cell. Per-cell controls live in a strip beside the cell, and
  pointer input (drag-to-swap, double-click expand) happens on the strip.
- **Injected scripts are embedded resources**, plain JS, idempotent (safe to
  run twice), and never send URLs, titles or page text back to the host - only
  structural facts (counts, booleans, `currentTime`).

## Gotchas

- **One WebView2 environment for everything.** Every cell and the sign-in
  window must be created with identical environment options (user-data folder
  and browser arguments), or creation fails. That shared profile is what makes
  one sign-in authenticate every cell.
- **Start with Windows and updates only act in an installed copy**
  (Velopack `IsInstalled`); dev, demo and `artifacts/publish` runs show no
  update banner.

## Release

Releases come from **this repo** with Velopack (`.github/workflows/release.yml`,
mirroring RingView): on every push to `main` it reads `<VersionPrefix>` from
`Directory.Build.props` and, if `v<version>` is not yet a release here, tests,
publishes win-x64 self-contained, runs `vpk pack`, and creates the release.
A push without a bump is a no-op; 0.0.0 never releases.

Flow: `./Scripts/bump-version.sh [Major|Minor|Patch]` -> commit -> push. CI
never bumps or commits. Check the published release before assuming a version
is unshipped (`gh release view v<version>`): the workflow is idempotent on the
version. Builds are unsigned (personal use), so SmartScreen may prompt.
