# StreamWall

An always-on Windows video wall for web video streams that need a sign-in:
many streams tiled across one or more borderless windows, each cell showing
only its video, all playing audio at once.

> **Status: early development.** The project skeleton is in place; the wall itself is not built
> yet. See [Docs/workplan.md](Docs/workplan.md).

## Develop

```bash
dotnet build StreamWall.slnx
dotnet test --solution StreamWall.slnx
dotnet run --project Src/StreamWall.App -- --demo   # placeholder streams, nothing written to disk
git config core.hooksPath .githooks                 # once: gitleaks pre-commit scan
```

## How it works

Each cell is an embedded web view (WebView2) on one shared browser profile,
trimmed down to its `<video>` element. You sign in once, by hand, in the app's
Browser tab (including any 2FA), and every cell is signed in. Find a stream in the same tab and
**Send to wall** puts it in any cell of any of your 1-3 wall windows. StreamWall
never sees, stores or automates your credentials.

## Privacy

Everything site-specific (stream URLs, the sign-in page, selectors) is entered
in the app and stored encrypted for your Windows account (DPAPI). Logs and
diagnostics never contain URLs, hosts or credentials: sites appear as stable
pseudonyms such as `site-7f3a9c`. Browsing history and cache are cleared
regularly; your signed-in session is kept. No telemetry.

## Requirements

- Windows 11 (or Windows 10 2004+), x64, with the WebView2 runtime (built into
  Windows 11)
- To build: the [.NET 10 SDK](https://dotnet.microsoft.com) (see `global.json`)

## Known limitations

- DRM-protected (Widevine) streams may not play in WebView2.

## License

[MIT](LICENSE). Personal-use project; not distributed.

## Docs

[DESIGN](Docs/DESIGN.md) - [TECHARCH](Docs/TECHARCH.md) - [workplan](Docs/workplan.md)
