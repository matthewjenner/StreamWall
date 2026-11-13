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
| M1    | Spike: check every TECHARCH "WebView2 requirement" with `NativeWebView`; video-only on a local test page; 12 cells measured | Not started |
| M2    | Scaffold (Klakr layout, Updatr/RingView standards): Core, Platform, App, tests, conventions, DiagLog/redactor/pseudonyms, config vault, gitleaks, CI | Not started |
| M3    | One cell: shared environment, sign-in window, video-only and audio scripts, control panel stream list (masked), demo site   | Not started |
| M4    | The grid: N x M walls, monitors, presets, cell strips, expand, drag-to-swap, global hotkeys, 12-cell measurement            | Not started |
| M5    | Resilience: signed-out detection + single prompt, stall watchdog, crash recovery, staggered loading, throttling verified    | Not started |
| M6    | Desktop and release: tray, single instance, profile hygiene, Velopack, release workflow, README numbers, v0.1.0             | Not started |

### M1 spike checklist

Run in `Spikes/` (throwaway, opts out of central packages as in RingView) with
local test pages only. Record each result with a date under "Spike results".

- [ ] `NativeWebView` exposes: environment options (user-data folder, browser
      arguments), `NewWindowRequested`, `ProcessFailed`, password
      autosave/autofill settings, document-created scripts in all frames, web
      messages, selective `ClearBrowsingDataAsync`.
- [ ] Two web views on one environment share cookies.
- [ ] Video-only styling works for: one video, several videos, a
      cross-origin iframe video, a page that swaps its video element.
- [ ] 12 cells playing: CPU, GPU, RAM measured; hardware decode confirmed.
- [ ] Playback with the app unfocused, occluded, and behind a borderless
      fullscreen app on another monitor; EcoQoS checked.
- [ ] Decision: `NativeWebView` or direct `Microsoft.Web.WebView2` hosting.

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

- Sign in once, including 2FA, in the sign-in window; every cell loads
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

None yet.

### Open questions

- Global hotkeys: Win32 `RegisterHotKey` (enough for a few fixed hotkeys, no
  hook) or SharpHook as in Klakr. Decide in M4.
- Auto-start on login (RingView has it): wanted?
- Releases: the app is personal use, not distributed. Keep RingView's Velopack
  release workflow (installs and auto-updates the user's own machine from this
  repo's releases), or only build locally with `Scripts/pack.sh`? Decide by M6.

### Decisions log

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
