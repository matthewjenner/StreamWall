# StreamWall - Technical Architecture

StreamWall is a .NET 10 + Avalonia 12 desktop app for Windows. Product design:
[DESIGN.md](DESIGN.md). Status and decisions: [workplan.md](workplan.md).

This describes the target architecture. Sections marked **(M1)** depend on the
spike results and are confirmed or revised there.

## Stack

- **.NET 10** (LTS; SDK pinned in `global.json`), C# `latest`, nullable,
  warnings as errors.
- **Avalonia 12** with **CommunityToolkit.Mvvm** (compiled bindings), Fluent
  theme. Windows 10 2004+ x64.
- **Web views:** `Avalonia.Controls.WebView` (`NativeWebView`, MIT), which
  uses the WebView2 Evergreen runtime on Windows. Fallback **(M1)**: host
  `Microsoft.Web.WebView2` directly if `NativeWebView` does not expose what is
  listed under "WebView2 requirements".
- **DPAPI** (`System.Security.Cryptography.ProtectedData`) for the config.
- **Velopack** for the installer and updates.
- **Tests:** xUnit v3 on Microsoft.Testing.Platform, AwesomeAssertions, fake
  time (`Microsoft.Extensions.TimeProvider.Testing`), Avalonia.Headless.
- Package versions are central in `Directory.Packages.props`.

### Versions

Latest stable on nuget.org, checked 2026-11-13. Check again before adding or
bumping.

| Package                                      | Version      | Notes                                         |
| -------------------------------------------- | ------------ | --------------------------------------------- |
| .NET SDK (`global.json`)                     | 10.0.403     | `rollForward: latestFeature`, MTP test runner |
| Avalonia, .Desktop, .Themes.Fluent           | 12.1.6       | RingView is on 12.1.5                         |
| Avalonia.Controls.WebView                    | 12.1.2       | MIT; depends on Avalonia >= 12.0.0            |
| Avalonia.Headless                            | 12.1.6       | Not `.XUnit` (fails on xunit.v3 4, see RingView) |
| AvaloniaUI.DiagnosticsSupport                | 2.2.3        | Debug only                                    |
| CommunityToolkit.Mvvm                        | 8.4.2        |                                               |
| System.Security.Cryptography.ProtectedData   | 10.0.15      | Stay on the 10.x line with .NET 10            |
| Velopack                                     | 1.2.161      |                                               |
| xunit.v3                                     | 4.0.3        |                                               |
| AwesomeAssertions                            | 9.6.0        | Apache-2.0 fork of FluentAssertions 7         |
| Microsoft.Extensions.TimeProvider.Testing    | 10.11.0      | Stay on the 10.x line with .NET 10            |
| Microsoft.Web.WebView2 (fallback only)       | 1.0.4314.35  | Only if M1 picks direct hosting               |

.NET 11 (STS) is out, but every local app is on .NET 10 LTS; StreamWall stays
there (see the decisions log).

## Projects

```
StreamWall.Core      Pure (net10.0, no packages, no P/Invoke). Config model and rules
                     (streams, site settings, walls, presets) and its JSON document;
                     LogRedactor + Pseudonymizer + DiagLog; the state machines:
                     StallWatchdog (per cell), SignedOutDetector (URL/selector match,
                     one prompt for many cells), StartupStagger, UpdateService (over an
                     IUpdater seam). Presentation: CellDisplay (strip label, status dot,
                     masked URL) as tested data.
StreamWall.Platform  DPAPI IConfigVault (atomic write of config.dat), AppPaths, the log
                     file, ProfileHygiene (selective data clearing), SingleInstance
                     (mutex + activation pipe), VelopackUpdater (Core's IUpdater).
StreamWall.App       Avalonia: AppHost composition root (real or --demo), the shared
                     WebView environment, CellHost (one web view + injected scripts),
                     WallWindow, CellStrip, SignInWindow, ControlPanelWindow, tray,
                     global hotkeys, screenshot mode, and the embedded demo site.
```

`Platform` and `App` target `net10.0-windows10.0.19041.0` (shared property in
`Directory.Build.props`, as in RingView).

## Threading

WebView2 objects belong to the UI thread, and every web view event (navigation,
web message, process failure) arrives there. So, unlike RingView's separate
core loop, StreamWall's Core state machines are driven **on the Avalonia UI
thread**: CellHost feeds them events, and their timers use the injected
`TimeProvider` with callbacks posted back via `Dispatcher.UIThread`. Disk I/O
(config, logs) is async and off the UI thread. Script calls are always awaited.

## WebView2 requirements (M1 checks each)

- **One environment:** a single user-data folder
  (`%LOCALAPPDATA%\StreamWall\WebView`) and one set of browser arguments for
  every cell and the sign-in window, so cookies are shared.
- **Browser arguments** against background throttling (each verified to have
  an effect in WebView2): `--autoplay-policy=no-user-gesture-required`,
  `--disable-background-timer-throttling`, `--disable-renderer-backgrounding`,
  `--disable-backgrounding-occluded-windows`. Also: never hide or suspend a
  controller while its wall is visible, and check whether Windows 11
  Efficiency mode (EcoQoS) throttles the WebView2 processes.
- **Hardware decode and GPU acceleration on.**
- **New windows:** `NewWindowRequested` intercepted; cells never open pop-ups,
  and a pop-up needed for sign-in is routed into the sign-in window.
- **Crash recovery:** `ProcessFailed` -> recreate the web view and reload.
- **Profile settings:** password autosave and general autofill off.
- **Scripts on every document, including iframes:** for video-only mode and
  signed-out detection.
- **Web messages** from injected scripts to the host (structural facts only).
- **Selective clearing:** `ClearBrowsingDataAsync` for history, disk cache,
  download history, autofill and session state, keeping cookies.
- **No Chromium logging, no crash dumps** where the API allows.

## Injected scripts

Embedded resources in `StreamWall.App`, plain JavaScript, idempotent:

- `video-only.js` - picks the target video (selector override, else the largest
  visible), leaves it in place in the DOM (moving it breaks React-style
  players), hides the rest with `visibility`, pins the video with
  `position: fixed; object-fit: contain` on black, and re-applies on DOM
  changes (MutationObserver). Interactive mode removes the styling. A video
  inside a cross-origin iframe is handled per frame, or by making the iframe
  fill the viewport **(M1)**.
- `audio.js` - applies `volume` / `muted` and re-applies when the element is
  swapped.
- `probe.js` - reports `currentTime`, video/iframe counts, selector match and
  the signed-out selector check. Never URLs, titles or text.

## Privacy mechanics

- **Config:** one JSON document (streams, site settings, walls, presets, the
  pseudonym key) encrypted with DPAPI CurrentUser in
  `%APPDATA%\StreamWall\App\config.dat`, written atomically. Logs live in
  `%APPDATA%\StreamWall\App\logs`.
- **Pseudonyms:** `site-` / `stream-` + the first 6 hex characters of
  HMAC-SHA256(key, normalized host or stream id), with a random 32-byte key
  kept in the config. Stable across runs, irreversible without the key.
- **LogRedactor:** every `DiagLog` line passes through it; it replaces
  URL-shaped strings, host-shaped strings and registered values (every
  configured URL and host) with pseudonyms or `[redacted]`. Unit tests feed it
  placeholder URLs in many shapes.
- **Repo guards:** `UrlAllowlistConventionTests` (no non-allowlisted host in any
  tracked file), gitleaks pre-commit (`.githooks`) and in CI over the full
  history, `.gitignore` for app data, WebView2 profiles and build output.

## Demo mode

`--demo` serves a fake site from embedded files through a WebView2 virtual
host (under `example.invalid`): a sign-in page, a signed-out redirect, pages
with one video, several videos, a video in an iframe, and a video that stalls
on demand, all using synthetic clips. It drives the real code paths with no
account, and it is the only thing screenshots (`--screenshot`) and UI reviews
use.

## Packaging and updates

As RingView: `dotnet publish` win-x64 self-contained, `vpk pack` into
`StreamWall-win-Setup.exe` plus update packages, released to this repo's GitHub
releases by `.github/workflows/release.yml` (idempotent on `VersionPrefix`,
never for 0.0.0). Installed copies check the releases without a token shortly
after launch and hourly. `Scripts/pack.sh` builds the same installer locally.
The WebView2 runtime is not bundled; Windows 11 ships it.
