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
- The Streams and Settings tabs show URLs masked (`https://*****/****`) with a
  per-row reveal toggle. The Browser tab shows its URL bar like any browser
  (it is the user's own screen). Window titles, the tray and crash dialogs
  show labels only.
- The signed-in session **persists** (cookies are kept) and refreshes normally
  while streams play. Browsing history, the disk cache, download history,
  autofill and session-restore data are cleared on startup, on exit and
  periodically. Disk-level encryption of the profile is not a goal.
- No telemetry. The only outbound call the app itself makes is the update check
  against this repo's public GitHub releases.

## Core UX

- **Browser tab.** The control panel has a Browser tab: one interactive web
  view on the shared profile with back, forward, reload, home and a URL bar.
  It is where the user signs in (including 2FA, by hand) and where they find
  streams, browsing the site as they would in Edge. The app never sees,
  stores or automates credentials or 2FA codes. Password saving and autofill
  are off in the profile. "Sign in again" opens this tab; once signed in,
  every signed-out cell reloads.
- **Send to wall.** On any page in the Browser tab, **Send to wall** opens the
  picker: a mini map of every wall (1-3) showing each cell's label or "empty".
  Clicking a cell sends the current page there; **Next empty cell** skips the
  map. Sending to a filled cell replaces it after a confirm. The page address
  comes from the web view; nothing is typed or copied. Every sent page is
  saved to the stream library automatically with an editable label (default:
  "Stream N"), so it can be re-sent from the picker or a preset later without
  browsing. Pasting a URL by hand remains as a fallback.
- **Clear a cell.** An **X** on the cell strip (and in the picker) empties the
  cell. The stream stays in the library.
- **Signed-out by pointing.** On the sign-in page, **This is the sign-in page**
  records its path as the signed-out URL pattern. A CSS selector indicator
  stays available as an advanced setting.
- **Video-only cells.** After each navigation the cell finds its video (the
  largest visible one, or a per-stream CSS selector), makes it fill the cell on
  black, hides everything else, and re-applies itself if the page swaps the
  element. A per-cell **interactive** toggle shows the full page (to dismiss a
  dialog or pick a quality) and then returns to video-only.
- **Audio from every cell at once** by default (no focus-to-hear). Per-cell
  volume (0-100) and mute, a master volume that scales them, and a master mute;
  all persisted and re-applied after reloads.
- **Signed-out handling.** A cell that hits a signed-out indicator (URL
  pattern and/or selector) stops reloading and shows **Signed out**; however
  many cells expire together, the user gets **one** "Sign in again" prompt,
  which opens the Browser tab at the sign-in page. On startup, if the first
  cell is signed out, that happens before the rest load.
- **Self-healing cells.** A stalled video (no `currentTime` progress for N
  seconds, default 20) reloads with backoff; a crashed web process is recreated.
  Neither runs while a cell is signed out or interactive. Cells load a few
  hundred ms apart so the site is never hit by 12 loads at once.
- **Walls (1-3 windows from the start).** The control panel spawns up to three
  walls, each its own borderless window with a rows x columns grid (1x1 to 4x4)
  on a chosen monitor: fullscreen on that monitor or a saved position and
  size, with always-on-top and borderless toggles. Walls are named ("Wall 1",
  renameable) so the picker can say where things go. Each wall can be opened,
  closed (its cells stop) or removed; open walls reopen on launch. Resizing a
  grid keeps each stream whose row and column still exist; the rest leave the
  wall (they stay in the library). Cells can be
  swapped by dragging their strips.
- **Cell strip.** A slim strip above or below each cell holds the controls
  (nothing can be drawn over a native web view): label, status dot
  (playing / stalled / signed out), volume, mute, reload, interactive toggle,
  expand, clear. Double-clicking a strip or a hotkey **expands** a cell to fill
  its wall; repeat to restore. A global hotkey hides every strip for a clean
  wall.
- **Presets.** Save and restore whole arrangements (walls, grids, cell-to-stream
  mapping, volumes). The last-used preset loads on launch.
- **Control panel.** Tabs: **Walls** (the 1-3 walls with their grids, add /
  open / close / remove, monitor and grid size; Start all / Stop all / Reload
  all; master volume and mute; session status), **Browser** (above), **Streams**
  (the library: label, masked URL, optional video selector, default volume;
  rename, remove, reorder, paste a URL), **Settings** (sign-in page, signed-out
  indicators, stall timeout, presets, Sign out & clear session, which wipes the
  profile after confirmation).
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
