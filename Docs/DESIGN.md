# StreamWall - Design

Product design, independent of the implementation details in
[TECHARCH.md](TECHARCH.md). Status and decisions: [workplan.md](workplan.md).

## What it is

StreamWall is a Windows desktop app that shows **many signed-in web video
streams at once**, tiled in a grid. It replaces a manual routine: open about
12 browser tabs on a site that needs a login, then start Picture-in-Picture in
each one.

It does not use Picture-in-Picture. Each cell is an embedded web view on a
shared, signed-in browser profile, trimmed down to its `<video>` element.

Primary target: Windows 11 x64 (Windows 10 2004+ works). No effort is spent on
macOS or Linux.

## Users and scale

One user, one site, roughly 12 streams. The UI is count-driven and never
hard-coded to 12: a wall is any grid from 1x1 to 4x4, and 1 to 3 walls run at
once (e.g. 12 cells on one screen, or 6 + 6 across two).

## Privacy model

The site and everything that identifies it are sensitive. The concern is
**readable output**: logs, diagnostics, error text, screenshots, and anything a
developer or AI assistant sees while working on the app. The repo is public.

- Every URL, selector and signed-out pattern is entered **in the app** and kept
  in one config file encrypted with Windows DPAPI (CurrentUser). Nothing
  site-specific exists in the repo.
- Logs and diagnostics name things by **pseudonym**: sites as `site-7f3a9c`,
  streams as `cell 3 stream-1b2c4d`. A pseudonym is stable across runs, so a
  problem can be followed through the logs, but cannot be turned back into a
  name. "Reset pseudonyms" issues new ones. User-given labels are never logged.
- The settings screen shows URLs masked (`https://*****/****`) with a per-row
  reveal toggle. Window titles, the tray and crash dialogs show labels only.
- The signed-in session **persists** (cookies are kept) and refreshes normally
  while streams play. Browsing history, the disk cache, download history,
  autofill and session-restore data are cleared on startup, on exit and
  periodically. Disk-level encryption of the profile is not a goal.
- No telemetry. The only outbound call the app itself makes is the update check
  against this repo's public GitHub releases.

## Core UX

- **Sign-in window.** A normal window with one interactive web view on the
  shared profile and a URL bar, opened at the configured sign-in page. The user
  signs in and completes 2FA by hand, then clicks **Done**: the window closes
  and every cell reloads. The app never sees, stores or automates credentials
  or 2FA codes. Password saving and autofill are off in the profile.
- **Video-only cells.** After each navigation the cell finds its video (the
  largest visible one, or a per-stream CSS selector), makes it fill the cell on
  black, hides everything else, and re-applies itself if the page swaps the
  element. A per-cell **interactive** toggle shows the full page (to dismiss a
  dialog or pick a quality) and then returns to video-only.
- **Audio from every cell at once** by default (no focus-to-hear). Per-cell
  volume (0-100) and mute, a master volume that scales them, and a master mute;
  all persisted and re-applied after reloads.
- **Signed-out handling.** The user can give a signed-out indicator: a URL
  pattern ("redirected to login"), a CSS selector that only exists on the login
  page, or both. A cell that hits it stops reloading and shows **Signed out**;
  however many cells expire together, the user gets **one** "Sign in again"
  prompt. On startup, if the first cell is signed out, the sign-in window opens
  before the rest load.
- **Self-healing cells.** A stalled video (no `currentTime` progress for N
  seconds, default 20) reloads with backoff; a crashed web process is recreated.
  Neither runs while a cell is signed out or interactive. Cells load a few
  hundred ms apart so the site is never hit by 12 loads at once.
- **Walls.** Each wall is a borderless window with a rows x columns grid on a
  chosen monitor: fullscreen on that monitor or a saved position and size,
  with always-on-top and borderless toggles. Streams are assigned to cells in
  settings, and cells can be swapped by dragging their strips.
- **Cell strip.** A slim strip above or below each cell holds the controls
  (nothing can be drawn over a native web view): label, status dot
  (playing / stalled / signed out), volume, mute, reload, interactive toggle,
  expand. Double-clicking a strip or a hotkey **expands** a cell to fill its
  wall; repeat to restore. A global hotkey hides every strip for a clean wall.
- **Presets.** Save and restore whole arrangements (walls, grids, cell-to-stream
  mapping, volumes). The last-used preset loads on launch.
- **Control panel.** The stream list (label, masked URL, optional video
  selector, default volume; add, edit, remove, reorder); site settings
  (sign-in page, signed-out indicators, stall timeout); walls and presets;
  Start all / Stop all / Reload all; master volume and mute; session status;
  Sign in, and Sign out & clear session (wipes the profile after
  confirmation).
- **Diagnostics view.** Per cell, structural facts only, e.g.
  `cell 3 stream-1b2c4d: videos=2 selectorMatched=0 iframes=1 signedOut=false advancing=true`.
  This is how video-only and signed-out problems get debugged without anyone
  seeing the real page.
- **Desktop citizen.** System tray with close-to-tray (walls keep running),
  single instance, About/version, in-app update banner. Dark-first.

## Known limitations

- DRM-protected (Widevine) streams may not play in WebView2. Documented, not
  solved.
- The site may limit concurrent streams per account; StreamWall cannot change
  that.
- Activity outside the app (DNS cache, network equipment) is out of scope for
  the privacy model.

## Negative effects considered

Twelve live web pages cost real CPU, GPU and RAM: one renderer process per cell
plus decode. Hardware decode stays on, cells that are stopped are torn down
rather than hidden, and the numbers for 12 cells are measured and published in
the README. Reloads and startup are staggered so the account is never hammered.
