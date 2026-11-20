# StreamWall - Work plan

The living build tracker: what phase we are in, what's done, what's next, and
the decisions log. Newest decisions go at the top of the log.

## Current effort: first build (v0.1.0)

Build StreamWall on the same stack and standards as the author's other desktop
apps (Klakr for the installed desktop app shape, Updatr and RingView for the
newest conventions): .NET 10, Avalonia 12, CommunityToolkit.Mvvm, xUnit v3 +
AwesomeAssertions, Velopack for installer and updates, releases on this repo.

Done means the acceptance checks below pass on the real site (run by the user,
not in any transcript) and v0.1.0 is released.

### Phases

Stop for review after each phase.

| Phase | Scope                                                                                                                       | Status      |
| ----- | --------------------------------------------------------------------------------------------------------------------------- | ----------- |
| M0    | Docs and repo prep: CLAUDE.md, DESIGN, TECHARCH, workplan, README, license, gitleaks (secrets + URL guard), CI              | Done        |
| M1    | Spike: check every TECHARCH "WebView2 requirement" with `NativeWebView`; video-only on a local test page; 12 cells measured | Done        |
| M2    | Scaffold (Klakr layout, Updatr/RingView standards): Core, Platform, App, tests, conventions, DiagLog/redactor/pseudonyms, config vault, gitleaks, CI | Done        |
| M3    | Browser and walls: shared environment, Browser tab (sign-in), 1-3 wall windows with N x M grids and monitor choice, Send to wall picker across walls, clear, stream library, video-only and audio scripts, demo site | Done        |
| M4    | Wall polish: presets, expand, always-on-top, global hotkeys, 12-cell measurement (swap, reload, icon strip and saved bounds done early) | In progress |
| M5    | Resilience: cell watchdog (stall or no video -> reload with backoff), one "cells have no video" notice, Reload all, crash recovery, throttling verified | Not started |
| M6    | Desktop and release: tray, profile hygiene, README numbers (single instance, Velopack, release workflow and v0.1.0 done early) | In progress |

### M1 spike checklist

`Spikes/StreamWall.WebViewSpike` (throwaway, opts out of central packages as
in RingView) runs against a local fake site under `example.invalid` only.
Generate its clip with `Spikes/gen-media.sh`, then run the exe with
`--mode checks` or `--mode load --cells 12 --flags all|autoplay|none --occlude`.
Cells are muted by WebView2 unless `--audible` is given.

- [x] Every WebView2 requirement is reachable (see results: Avalonia for
      hosting and environment options, the WebView2 SDK for the rest).
- [x] Web views on one environment share one browser process and cookies.
- [x] Video-only works for one video, several videos, a cross-origin iframe
      player, and a page that re-creates its video element.
- [x] 12 cells playing: CPU, GPU, RAM measured; hardware decode confirmed.
- [x] Playback unfocused and fully covered by a topmost borderless window.
- [ ] EcoQoS / Efficiency mode over long runs, and a real game on another
      monitor: carried into M5 (needs the user's machine and a soak).
- [x] Decision: hybrid (below).

### What we know about the target site

Recorded generically on purpose (no names, hosts or identifying markup).

- Streams play in Microsoft Edge, which is what WebView2 is built on, so the
  DRM limitation is not expected to apply.
- The `<video>` is wrapped by a common HTML5 player library. The library sets
  inline styles on the element (so video-only styles need `!important`, and
  may need to target the player's wrapper too), draws its own control bar and
  poster over the video (hidden by video-only mode), and may restore its own
  volume (so `audio.js` re-applies ours on `volumechange`).
- The element carries `autoplay` and has a stable id and test attribute, so a
  per-stream selector override is easy if "largest visible" ever picks wrong.
- From the first real run (2026-11-13, redacted log only): the video is in the
  main document, not an iframe; pages carry 1-5 other iframes with no video,
  which the picker correctly ignores. Every stream reported a 640 px video
  width (open: the stream's real size, or the player choosing low quality for
  a small cell). The player autoplays with the element muted, so the script
  unmutes it (see M3 notes).
- The session persisted across restarts: cells played on relaunch without
  signing in again.

### Acceptance checks (v0.1.0)

With streams configured in the app by the user:

- Sign in once, including 2FA, in the Browser tab; every cell loads
  signed in.
- Every cell shows only its video, and all of them play audio at once with
  independent volumes.
- Playback stays smooth while a borderless-fullscreen game has focus on
  another monitor.
- When the session expires there is one prompt; after signing in again every
  cell recovers.
- After a restart, history and cache are cleared and the session is still
  signed in.
- `git log -p`, the source tree, the logs and the diagnostics view contain no
  real URLs, hosts or credentials; sites appear only as pseudonyms.

### Spike results (M1)

- **2026-11-13 - Hybrid hosting works.** Avalonia.Controls.WebView 12.1.2 on
  Avalonia 12.1.6 hosts WebView2 (runtime 157.0.4341.46).
  `EnvironmentRequested` (as `WindowsWebView2EnvironmentRequestedEventArgs`)
  sets the user-data folder and browser arguments; `AdapterCreated` gives an
  `IWindowsWebView2PlatformHandle`, and
  `CoreWebView2.CreateFromComICoreWebView2` (Microsoft.Web.WebView2
  1.0.4314.35) wraps it for everything else: profile password autosave and
  autofill off (read back false), document-created scripts (they run in
  cross-origin iframes too), `FrameCreated` + per-frame web messages,
  `NewWindowRequested` (both Avalonia's and the SDK's fire; `window.open` and
  `target=_blank` blocked), `ProcessFailed`, `IsMuted`, selective
  `ClearBrowsingDataAsync`, `CapturePreviewAsync`, and virtual host mapping
  for the demo site (`*.example.invalid` works). Avalonia cannot be handed an
  SDK-created environment without reflection, so `IsCustomCrashReportingEnabled`
  is not settable; M6 deletes local crash dumps under the profile instead.
- **2026-11-13 - Capability checks (8 cells, one per test page) all pass.**
  One browser process for all cells; a cookie set in one cell is seen by a cell
  created 6 s later, and survives `ClearBrowsingDataAsync` (history, disk cache,
  downloads, autofill, password data) plus a reload. Volume is enforced against
  a player that restores its own volume (configured 0.25-0.6, read back
  exactly). Signed-out detection works by URL pattern on `NavigationStarting`
  and by selector in the probe. A renderer killed via DevTools `Page.crash`
  raised `ProcessFailed` (RenderProcessExited); `Reload()` had video playing
  again 3 s later. The interactive toggle shows the full page and returns to
  video-only.
- **2026-11-13 - Bug found and fixed in video-only:** re-picking the target
  after load measured the already-fullscreen target at viewport size, so a
  larger chat iframe won. `pick()` now measures with the takeover class off.
- **2026-11-13 - 12 cells of 1080p30 H.264 (4.5 Mbps), 4x3 in 1280x720, 32
  logical CPUs, 144 Hz display:** every cell at 1.00x real time with 0 dropped
  frames out of ~6,800 per 20 s phase, in all phases: focused, unfocused (another
  window active), and fully covered by a topmost borderless window. The page
  stayed `visible` and timers held 1 Hz throughout. GPU video-decode engine
  ~40% (hardware decode confirmed), 3D ~2.5%, ~1.1-1.3 GB private memory across
  18 processes, CPU 0.5-4 cores (noisy, and it includes the spike's own
  GPU-counter sampling; measure properly in M4). With only the autoplay flag,
  results were the same: the three anti-throttling flags made no measurable
  difference here. Keep them (they cost nothing) and re-check on long runs.
- **2026-11-13 - Build notes for M2:** an `app.manifest` declaring Windows
  10/11 support is required (without it Avalonia throws "Unable to create
  child window for native control host"). The WebView2 package references its
  WPF assembly, producing MSB3277 (WindowsBase conflict); strip that reference
  in the real projects so warnings-as-errors stays clean.

### Scaffold (M2)

- Solution `StreamWall.slnx`: Core (pure), Platform (DPAPI config store, log
  file, paths), App (AppHost composition root, control-panel shell), and three
  test projects. 76 tests, zero build warnings, Debug and Release.
- Conventions ported from RingView: ASCII, US English (word list verbatim, no
  vendor exceptions yet), Core purity. New: `UrlAllowlistConventionTests`,
  which reads the gitleaks URL rule and its allowlist from `.gitleaks.toml`.
- The config is one DPAPI-encrypted JSON document. A file that cannot be
  decrypted (another Windows account) or parsed is moved to
  `config.dat.unreadable-<stamp>`, never overwritten, and the app starts empty.
  Every load and save re-registers every configured URL with the redactor
  before anything can log it.
- Found while testing: the generic `key=value` rule ran before the Bearer rule
  and left the token in `Authorization: Bearer <token>`; token shapes now run
  first, and cookie headers lose their whole value. The gitleaks URL rule
  treated `user:pass@` as the host; it now skips userinfo and checks the real
  host.
- Deferred to where they are used: the WebView2 package (and stripping its
  WPF reference) to M3; Velopack, single instance and the icon to M6.

### Open questions

- Global hotkeys: Win32 `RegisterHotKey` (enough for a few fixed hotkeys, no
  hook) or SharpHook as in Klakr. Decide in M4.
- Auto-start on login (RingView has it): wanted?
- Releases: the app is personal use, not distributed. Keep RingView's Velopack
  release workflow (installs and auto-updates the user's own machine from this
  repo's releases), or only build locally with `Scripts/pack.sh`? Decide by M6.

### M3 checklist

- [x] Core wall model: walls (max 3) with rows x columns cells holding stream
      ids; assign, clear, next empty cell across walls, resize keeps each
      stream whose row and column survive; stream library add-or-reuse by
      URL (case, trailing slash and fragment ignored). Pure and tested.
- [x] Web view host: one shared environment (AppPaths.WebView, browser
      arguments), SDK wrap, profile settings, new-window blocking; WebView2's
      WPF reference stripped (MSB3277).
- [x] Browser tab: back / forward / reload / URL bar, Send to wall
      (Home and "This is the sign-in page" later removed; see decisions).
- [x] Wall windows: 1-3, each borderless on a chosen monitor (fullscreen),
      N x M cells with a minimal strip (label, mute, volume, clear).
- [x] Picker: every wall's grid, filled vs empty, replace confirm, next empty.
- [x] Cells: video-only + audio scripts (from the spike, with its fix),
      staggered start.
- [x] Streams tab: library with masked URLs, reveal, rename, remove, paste URL.
- [x] `--demo` serves the fake site (from the spike) via virtual host; UI tests
      cover the model and the picker; screenshots of demo only.

### Browser and walls (M3)

- The control panel has Walls / Browser / Streams / Settings tabs (stacked
  pages, not a TabControl, so the Browser's web view survives tab switches).
  Send to wall opens `PickerWindow` (its own window: nothing draws over the
  Browser's web view) with every wall's map, a replace confirm and Next empty
  cell; with no walls it offers to add one.
- `WallManager` keeps one `WallWindow` per open wall and updates cells in
  place, so only a cell whose stream changed reloads. Cells start at least
  300 ms apart. Closing a wall window marks it closed; exiting does not.
  Non-fullscreen walls are a window at 60% of the monitor until saved bounds
  (M4).
- Changes apply at once and save 400 ms later (a slider drag is one write);
  new URLs are registered with the redactor before the save. Exit flushes.
- Host-level mute (`CoreWebView2.IsMuted`) for per-cell and master mute;
  volume goes to the page and every frame as a web message, which the
  video-only script applies (and re-applies when the player fights back).
- `--demo` serves `Src/StreamWall.App/Web/DemoSite` (canvas-drawn silent video,
  no media files) on `demo.example.invalid` with a demo-only WebView2 profile
  (`%LOCALAPPDATA%\StreamWallData\DemoWebView`). `--demo --screenshot <folder>`
  sends a stream through the real Send to wall path, saves PNGs of every cell,
  the Browser page and the Walls tab, and quits; its wall is windowed so it
  never covers the monitor. First run (2026-11-13): all four cells "playing",
  video-only correct including the cross-origin iframe player.
- WebView2's WPF/WinForms references are dropped repo-wide in
  `Directory.Build.targets` (they flow into the test project too).
- Found and fixed: the Walls tab's monitor ComboBox lost its selection when the
  monitor list arrived after the row (now re-applied after the items update,
  and a transient -1 is never saved).
- First real-site run (2026-11-13): 2 walls, 5 streams sent, a replace and a
  clear; every cell "playing" about 2 s after loading. Found and fixed:
  - **No sound.** The site autoplays with the `<video>` muted; the script only
    set volume. It now also unmutes the element whenever the player re-mutes
    it; muting is solely the host's (`CoreWebView2.IsMuted`). The demo player
    now autoplays muted too.
  - **Quitting marked every wall closed.** The lifetime closed the wall windows
    before the Exit event, which read as the user closing them. And the exit
    save blocked the UI thread on a save that needed it (5 s hang, last change
    lost). `Shutdown.Wire` now handles the control panel's Closing: cancel,
    close walls as shutdown, await the save, then close (`ShutdownUiTests`).
  - A cell switching streams briefly logged "no video" (the previous page's
    probe time was reused); reset on reload.
- Cell diagnostics: status changes are logged with the probe's facts (video
  and iframe counts, which frame has the target, width, paused, element muted,
  volume, host muted, signed out), and audio changes once settled. This is
  how the real site is debugged without seeing it.
- Not yet: signed-out detection and the "Sign in again" prompt (M5), presets,
  expand, drag-to-swap, hotkeys, always-on-top (M4), Sign out & clear session
  (M6).

### Browser tabs, zoom, Open in Browser (v0.2.0)

- **Tabs:** `BrowserViewModel` holds `BrowserTabViewModel`s; `BrowserView`
  draws the strip and keeps one `BrowserTabView` (web view) per tab, showing
  only the selected one. Restored tabs load when first shown; a tab opened
  from a link loads at once in the background. New-window requests and the
  context menu's "Open link in new window" (replaced by a custom "Open link in
  new tab" item) open tabs. The tab list is saved as it changes
  (`BrowserRules.WithTabs`) and restored by `BrowserRules.StartTabs`. A tab's
  placeholder "New tab" title is never used as a stream label (found by a
  test: it is `PageTitle` vs the strip's `Title`).
- **Zoom per site:** `BrowserRules.ZoomFor` / `WithZoom` (host-keyed, 25-500%,
  1.0 not stored), applied on every navigation, saved on every change.
  `ControllerZoom` reads and sets the controller's `ZoomFactor` through a
  self-declared COM interface: the SDK's raw interfaces are embedded interop
  types (`[TypeIdentifier]`), which C# cannot reference, and Avalonia's
  controller pointer cannot be wrapped by the SDK. Changes are noticed by
  reading the factor every 500 ms: `ZoomFactorChanged` never reached a
  self-declared handler. Verified in `--demo --screenshot` (130% saved and
  restored in a new tab of the same site; reset forgets the site).
- **Open in Browser:** `WallViewModel.OpenInBrowserAsync` per the setting;
  `OpenInBrowserDialog` for "Ask each time"; the control panel comes forward
  on the new tab (`MainWindowViewModel.ShowRequested`).
- Settings: Restore tabs, Focus new tabs, Remember zoom (+ Forget saved zoom
  levels), Open in Browser mode. Every one has normalization, round-trip and
  UI tests (`BrowserRulesTests`, `BrowserTabsUiTests`).
- Headless tests set `WebEnvironment.Enabled = false`, so the real Browser
  tab, its strip and walls with streams run in tests without WebView2.

### Decisions log

- **2026-11-20** - v0.2.1 from using v0.2.0: a **pin** in the control panel's
  header (and a Settings switch, kept in step) sets `Topmost`, so the panel is
  never lost behind a wall. Settings > Updates: version, status
  (`UpdateStatusText`, tested for every state) and Check for updates. Tabs:
  fixed width (**Tab width** setting, 120-360, default 200), the speaker slot
  always reserved (opacity, not visibility), headers cached and updated in
  place instead of the strip being rebuilt on every title change, the scroll
  bar hidden with the wheel scrolling sideways, and the selected tab brought
  into view.

- **2026-11-20** - The control panel remembers its placement (user request,
  in v0.2.0 before release): `WindowPlacement` + `WindowPlacementRules`
  (pure, unit-tested for every safety case) and a **Remember the control
  panel's position** setting. Placement is restored after the config loads
  (so the window may move once at startup). The home monitor is the one
  holding most of the title bar; a straddling window belongs to the monitor
  with more of it (a first version picked the first match, caught by a test).

- **2026-11-20** - v0.1.4 from user requests: stream labels from page titles,
  with a **Stream labels** setting (page title + fill "Stream N" labels by
  default; page title new only; follow the page title; numbered). A label
  typed in Streams is marked `LabelEdited` and never replaced. A **swap**
  button (picker in swap mode, any wall, no confirm) replaces drag-to-swap;
  **reload** per cell and **Reload all** (staggered). Strip controls are
  standard Windows icon buttons (Segoe Fluent Icons / MDL2 glyphs) with
  tooltips, per the user's preference for options and icons over menus.
  `AppHost.Update` skips changes that return the same config (title reports).

- **2026-11-13** - From using v0.1.2 (released as v0.1.3): a windowed wall is a
  normal window (title bar, movable, resizable) that saves its bounds after a
  move or resize settles (the borderless "window" could not be moved; saved
  bounds were pulled forward from M4). New streams start at volume 1, with a
  "Volume for new streams" setting. Walls open without focus and the control
  panel comes to the front after they load (it opened behind a fullscreen wall
  after an update). Debounces use `Task.Delay`, not `DispatcherTimer`: headless
  UI tests do not run dispatcher timers.

- **2026-11-13** - "Dev copy" means "not the Velopack-installed copy" (decided
  at startup by `IsInstalled`), not "Debug build" (v0.1.2). A Release build run
  from the repo or `artifacts/publish` would otherwise have used the installed
  app's config and profile. Only the installed copy touches `StreamWall\App` and
  `StreamWallData\WebView`; development works only with dev data.

- **2026-11-13** - Browser profiles moved to `%LOCALAPPDATA%\StreamWallData\`
  (v0.1.1). They were under `%LOCALAPPDATA%\StreamWall`, which is Velopack's
  per-user install folder: v0.1.0's installer refused to install ("already
  installed") and an uninstall would have deleted the signed-in profile. The
  existing profile was moved by hand on the dev machine; v0.1.0 should not be
  installed. `NoDataLivesInVelopacksInstallFolder` pins the rule. (RingView
  avoided this by keeping its data under `%APPDATA%`.)

- **2026-11-13** - Release early: v0.1.0 ships after M3 (not after M6) so an
  installed copy can run while development continues, and the updater gets
  exercised from the start. Milestones continue as 0.x releases; bump when a
  user-visible change is ready. Velopack, the release workflow, `pack.sh` and
  the single-instance guard come from RingView. Debug builds are dev copies
  with their own data folders (`StreamWall\Dev`, `StreamWallData\DevWebView`) and
  single-instance names, so they never share config or session with the
  installed app; the installed copy keeps `StreamWall\App` (where the first
  real-site config already lives). No icon yet: the installer uses Velopack's
  default until one is made.

- **2026-11-13** - No sign-in configuration (user decision). Signing in is just
  the Browser tab, like any browser; the shared profile carries the session to
  every cell, and the target sites rarely expire sessions (refresh tokens).
  Removed: "This is the sign-in page", the Home button, and the signed-out URL
  pattern / selector settings. Recovery is generic instead (M5): a cell with no
  progress or no video reloads with backoff, several stuck cells produce one
  notice, and Reload all retries now; after signing in again the cells recover
  on their own. It cannot tell "signed out" from "offline", which is fine for
  a wall. The Browser reopens at its last page (saved, redacted like any URL).

- **2026-11-13** - Streams are picked by browsing, not typing: the control
  panel's Browser tab (shared profile) is where the user signs in and finds
  streams, and **Send to wall** puts the current page into a cell chosen on a
  map of every wall. Sent pages join the stream library automatically. This
  replaces the separate sign-in window and the paste-a-URL stream list (kept
  as a fallback). The signed-out URL pattern is set by pointing at the sign-in
  page. Browser is a tab (can pop out later).
- **2026-11-13** - Multiple walls (1-3 windows) are in the model and the
  picker from M3, not added in M4, so nothing is built single-wall first.

- **2026-11-13** - Logs never name streams by label (users can type anything
  into a label); cell index and stream pseudonym only. The redactor also hides
  each configured host's distinctive labels (the site's name) in any case, and
  leaves generic labels (`live`, `www`, `cdn`, ...) readable.
- **2026-11-13** - Version stays 0.0.0 (never released) until v0.1.0 is ready.

- **2026-11-13** - Web views are hosted by Avalonia's `NativeWebView` and
  driven through the official WebView2 SDK wrapped around its COM pointer
  (M1 results). Not direct WebView2 hosting: Avalonia already handles the
  child-window hosting, layout and DPI, and nothing needed is missing.
- **2026-11-13** - Spike and demo media are silent by default (host-muted;
  `--audible` to hear them) and use a quiet tone, after the first runs played a
  loud 440 Hz tone from 12 cells.

- **2026-11-13** - MIT license (personal use, not distributed). No push to
  GitHub until CI is in place; CI now runs gitleaks over the full history,
  including a `non-placeholder-url` rule that fails on any URL whose host is
  not on the allowlist in `.gitleaks.toml`. The .NET job activates once the
  solution exists (M2).

- **2026-11-13** - Docs mirror RingView/Klakr (`CLAUDE.md`, `Docs/DESIGN.md`,
  `TECHARCH.md`, `workplan.md`) instead of the original brainstorm prompt,
  which is not committed.
- **2026-11-13** - .NET 10 LTS, not .NET 11 (STS, now released): every local
  app is on .NET 10 and the SDK pin matches RingView. Avalonia 12.1.x, the
  newest line (RingView, Updatr, FleaTrackr), rather than Klakr's 12.0.x.
- **2026-11-13** - The repo is public (Velopack update checks read public
  releases without a token, as in RingView). So nothing site-specific is ever
  committed, placeholders use `example.invalid`, and a convention test fails
  on any non-allowlisted host.
- **2026-11-13** - Privacy targets readable output: logs and diagnostics use
  stable pseudonyms (HMAC with a per-install key), never names or labels. The
  session persists; history and cache are cleared. No disk encryption of the
  WebView2 profile (user decision).
- **2026-11-13** - Embedded web views on one shared profile, not browser
  Picture-in-Picture. Credentials and 2FA are only ever typed by the user in
  the sign-in window.
