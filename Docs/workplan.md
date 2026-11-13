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
| M3    | Browser and walls: shared environment, Browser tab (sign-in), 1-3 wall windows with N x M grids and monitor choice, Send to wall picker across walls, clear, stream library, video-only and audio scripts, demo site | Not started |
| M4    | Wall polish: presets, full cell strips, expand, drag-to-swap, always-on-top / borderless / saved bounds, global hotkeys, 12-cell measurement | Not started |
| M5    | Resilience: signed-out detection + single prompt, stall watchdog, crash recovery, staggered loading, throttling verified    | Not started |
| M6    | Desktop and release: tray, single instance, profile hygiene, Velopack, release workflow, README numbers, v0.1.0             | Not started |

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
- Still unknown: whether the player sits inside an iframe. M1 covers both.

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
- [ ] Web view host: one shared environment (AppPaths.WebView, browser
      arguments), SDK wrap, profile settings, new-window blocking; WebView2's
      WPF reference stripped (MSB3277).
- [ ] Browser tab: back / forward / reload / home / URL bar, Send to wall,
      This is the sign-in page.
- [ ] Wall windows: 1-3, each borderless on a chosen monitor (fullscreen),
      N x M cells with a minimal strip (label, mute, volume, clear).
- [ ] Picker: every wall's grid, filled vs empty, replace confirm, next empty.
- [ ] Cells: video-only + audio scripts (from the spike, with its fix),
      staggered start.
- [ ] Streams tab: library with masked URLs, reveal, rename, remove, paste URL.
- [ ] `--demo` serves the fake site (from the spike) via virtual host; UI tests
      cover the model and the picker; screenshots of demo only.

### Decisions log

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
