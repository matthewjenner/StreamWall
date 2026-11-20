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

- Every URL and selector is entered **in the app** (or captured from the Browser
  tab) and kept
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

- **Browser tab.** The control panel has a Browser tab with **browser tabs**,
  all on the shared profile, plus back, forward, reload and a URL bar. It is
  where the user signs in (including 2FA, by hand), exactly as in Edge, and
  where they find streams. Every cell shares that session, so there is no
  sign-in setup: no sign-in page, pattern or selector to configure. The app
  never sees, stores or automates credentials or 2FA codes. Password saving
  and autofill are off in the profile.
  - **Tabs:** a strip with each tab's title, a speaker button while it plays
    audio (click to mute it), and a close button; middle-click closes; +
    opens one. Middle-click, Ctrl+click and new-window links open a tab, and
    the right-click menu offers "Open link in new tab". Background tabs keep
    running, so several streams can play in tabs (some sites pause hidden
    video). Closing the last tab leaves a fresh one. Settings: **reopen all
    tabs on launch** (default) or start with one tab at the last page;
    **tabs from links load in the background** (default) or come to the front.
  - **Zoom per site:** Ctrl+wheel, Ctrl+plus/minus or the toolbar's - 100% +
    (click the percentage to reset). The level is remembered per site and
    restored in any tab that opens one of its pages. Settings: **remember zoom
    per site** (default on) and **Forget saved zoom levels**.
  - **Open in Browser** (a wall cell's strip button) opens the cell's page in
    a new tab and brings the control panel forward on it. Setting: **keep it
    on the wall** (default), **move it off the wall** (it stays in the
    library), or **ask each time**.
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
- **Video-only cells.** After each navigation the cell finds its video (the
  largest visible one, or a per-stream CSS selector), makes it fill the cell on
  black, hides everything else, and re-applies itself if the page swaps the
  element. A per-cell **interactive** toggle shows the full page (to dismiss a
  dialog or pick a quality) and then returns to video-only.
- **Audio from every cell at once** by default (no focus-to-hear). Per-cell
  volume (0-100) and mute, a master volume that scales them, and a master mute;
  all persisted and re-applied after reloads. New streams start at a quiet
  **Volume for new streams** (Settings, default 1), so an unexpectedly loud
  stream never blasts.
- **Self-healing cells, no sign-in detection.** A cell whose video stops
  advancing, or that has no video at all (an expired session lands on a login
  page; a stream goes offline; the site is down), shows it on its strip and
  reloads with backoff (default stall timeout 20 s, backing off to a few
  minutes). The app does not try to tell "signed out" from "offline": if
  several cells are stuck at once, the control panel says so once ("3 cells
  have no video - if the site signed you out, sign in again in the Browser
  tab"), and **Reload all** retries every cell now. After the user signs in
  again, the next retry picks up the session and cells recover on their own.
  Sessions on the target sites rarely expire (refresh tokens), so this is the
  rare path. A crashed web process is recreated. No retries while a cell is
  interactive. Cells load a few hundred ms apart so the site is never hit by
  12 loads at once.
- **Walls (1-3 windows from the start).** The control panel spawns up to three
  walls, each its own window with a rows x columns grid (1x1 to 4x4) on a
  chosen monitor: **fullscreen** (borderless, the whole monitor) or **window**
  (a normal window with a title bar, movable and resizable, that remembers
  where it was put; changing its monitor recenters it), with an always-on-top
  toggle (M4). Walls open without taking focus, and the control panel comes to
  the front once they are up. Walls are named ("Wall 1",
  renameable) so the picker can say where things go. Each wall can be opened,
  closed (its cells stop) or removed; open walls reopen on launch. Resizing a
  grid keeps each stream whose row and column still exist; the rest leave the
  wall (they stay in the library).
- **Cell strip.** A slim strip above each cell holds the controls (nothing can
  be drawn over a native web view): number, label, status dot (playing /
  waiting / no video / error), then one-click icon buttons with tooltips, using
  standard Windows icons: mute, a volume slider, **reload**, **swap**, **open
  in Browser** and
  **clear**. (Planned: interactive toggle, expand.)
  Double-clicking a strip or a hotkey **expands** a cell to fill its wall
  (M4). A global hotkey hides every strip for a clean wall (M4).
- **Swap.** The strip's swap button opens the picker titled "Swap 'X' with:",
  with the cell itself marked. One click on any other cell, on any wall,
  trades the two (no confirm: nothing is lost); an empty cell receives the
  stream, and "Move to next empty cell" does that in one click. Only the two
  cells reload. This replaces drag-to-swap, which could not cross walls.
- **Stream labels (Settings).** By default a stream is named after its page's
  title when it is sent, and a label still "Stream N" takes its page's title
  the next time its cell loads. Other choices: page title for new streams
  only; keep following the page title as it changes; or numbered "Stream N".
  A label typed in the Streams tab is the user's and is never replaced.
- **Reload all** on the Walls tab reloads every cell, staggered like startup.
- **Presets.** Save and restore whole arrangements (walls, grids, cell-to-stream
  mapping, volumes). The last-used preset loads on launch.
- **Control panel.** Tabs: **Walls** (the 1-3 walls with their grids, add /
  open / close / remove, monitor and grid size; Start all / Stop all / Reload
  all; master volume and mute; session status), **Browser** (above), **Streams**
  (the library: label, masked URL, optional video selector, default volume;
  rename, remove, reorder, paste a URL), **Settings** (stall timeout, presets,
  pseudonyms, Sign out & clear session, which wipes the profile after
  confirmation).
- **Diagnostics.** Per cell, structural facts only, logged on every status
  change, e.g. `wall 1 cell 3 stream-1b2c4d: playing (top: videos=1 iframes=2
  target=yes width=640 paused=no elementMuted=no volume=0.50 hostMuted=no)`.
  This is how video-only and playback problems get debugged without anyone
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
