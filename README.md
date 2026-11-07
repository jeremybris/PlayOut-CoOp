# PlayOut Co-Op

PlayOut Co-Op (`PlayOutCoop.exe`) is a headless split-screen launcher built from the engine of
**[Nucleus Co-op](https://github.com/SplitScreen-Me/splitscreenme-nucleus)**. It runs Nucleus Co-op game handlers
(the `handlers\*.js` scripts from the Nucleus hub) without the Nucleus user interface. The PlayOut desktop app drives
it through a small command-line / JSON contract ([CONTRACT.md](CONTRACT.md)).

This repository contains:

| Path | What it is | Origin |
|---|---|---|
| `src/Nucleus.Gaming` | The engine: handler loading (Jint 2.11.58), `GenericGameInfo`, `GenericContext`, `GenericGameHandler`, `GameProfile`, `UserScreen`, `PlayerInfo`, `Tools/*` | Nucleus Co-op `Master/NucleusGaming`, modified (see below) |
| `src/Nucleus.Gaming/Headless` | New code: `EngineHost`, `EngineUI`, `HeadlessDevices`, `GameProfile.CreateHeadless`, `HeadlessSession` | PlayOut Co-Op |
| `src/WindowScrape` | Window helper library used by the engine | Nucleus Co-op `Master/ThirdParty/WindowScrape`, unchanged |
| `src/Nucleus.Inject` | The injector, built twice as `Nucleus.IJx86.exe` and `Nucleus.IJx64.exe` | Nucleus Co-op `Master/Nucleus.Inject`, unchanged source |
| `src/StartGame` | `StartGame.exe` (mutex killing, symlink/hardlink builder, hooked start) | Nucleus Co-op `Master/StartGame`, unchanged source |
| `ThirdParty/easyhook` | EasyHook managed/native binaries (MIT) referenced at build time | Nucleus Co-op `Master/ThirdParty/easyhook` |
| `test/CoopDummy`, `test/EngineSmoke`, `test/handlers` | A dummy game, an engine smoke test and test handlers (copy, hard link, symlink, symlinked folders, hooks, prompts) | PlayOut Co-Op (not shipped) |
| `CONTRACT.md` | The PlayOut ⇄ PlayOutCoop.exe interface | PlayOut |
| `ENGINE-NOTES.md` | How to call the engine from the CLI | PlayOut Co-Op |

The native pieces (`Nucleus.Hook32/64.dll`, `Nucleus.SHook32/64.dll`, EasyHook, ProtoInput, `openxinput1_3.dll`,
`SDL2.dll`, `utils\**`) are **not** built here. They come from the official Nucleus Co-op release
(`NucleusApp.zip`) and are placed in the engine root at runtime.

## Credits

Nucleus Co-op is © its authors and contributors, as credited upstream: the original Nucleus Co-op project by Lucas Assis
(lucasassislar); the Nucleus Co-op Alpha 8 Mod by ZeroFox; Proto Input, USS, multiple keyboards/mice and hooks by
Ilyaki; official Nucleus Co-op 2.0 and up by Mikou27; website and handler API by r-mach; handler development and
testing by Talos91, PoundlandBacon, Pizzo, maxine64 and many more, all part of the SplitScreen.Me organization. See the
Nucleus Co-op README for the full credits, including the third-party utilities it uses. WindowScrape, EasyHook (MIT), Jint (BSD-2), SharpDX (MIT),
DotNetZip (MS-PL) and Newtonsoft.Json (MIT) are used under their own licenses. `Coop/Generic/Cursor/*` keeps its
original GPL notice (Gerald Evans).

PlayOut Co-Op is not affiliated with or endorsed by the Nucleus Co-op project.

## License

GPL-3.0, the same license as Nucleus Co-op. See [LICENSE](LICENSE). The source of every binary we ship is this
repository at the matching commit, plus Nucleus Co-op upstream at commit
`3d22c0e8a6dd624f08a5cce286cd594c8abf264d` (8 July 2026) for the files listed above as unchanged.

## Changes from Nucleus Co-op

Imported from upstream commit `3d22c0e` (the commit "Import Nucleus Co-op engine sources" in this repository is the
verbatim import, so `git diff 51fb005` shows every change). Each modified upstream file starts with a dated
"Modified for PlayOut Co-Op" notice.

- **Build:** SDK-style projects targeting .NET Framework 4.8 (upstream: classic projects, 4.7.2), built with
  `dotnet build`. Same sources, same compile constants (`ALPHA;UNSAFE;RELEASE` for Release, `TRACE` for the
  injector and StartGame). The unused `IWshRuntimeLibrary` COM reference was dropped. Nucleus.Inject is built as two
  projects (`Nucleus.IJx86`, `Nucleus.IJx64`) instead of one project with a post-build rename.
- **Headless UI (`Headless/EngineUI.cs`):** every place where the engine showed a `MessageBox`, a
  `NucleusMessageBox`, a "Press OK when…" `Prompt`, the launcher-exe file picker, a handler-defined `CustomPrompt`,
  the `ProcessPicker`, or an OSD message (`Globals.MainOSD.Show`) now calls `EngineUI`. With `EngineUI.Headless`
  false the original Nucleus window is shown exactly as before; with it true the request is logged, passed to an
  injectable callback, and otherwise auto-answered with the dialog's default button. The shortcut-reminder overlays
  are not shown when headless; the split-screen background windows (`WPFDiv`) remain optional.
- **Configurable install root:** `Globals.NucleusInstallRoot` is settable (default unchanged: the entry exe's folder).
  `Globals.ini` (Settings.ini), `ThemeFolder`, `GameProfilesFolder`, the HubCache/webview, SDL controller DB,
  WindowMerger, WPFDiv, ShortcutsReminder and GameManager asset paths, and the `utils\backup` / Steamless paths that
  used `Application.StartupPath` or the current directory now follow it.
- **Engine API additions:** `GameManager(bool scanHandlers)` and `GameManager.LoadHandlerFile`,
  `GenericGameInfo.LoadError`, the `GenericGameHandler.PlayFinished` event, a progress hook in the per-player loop,
  log forwarding from `GenericGameHandler.Log` and `LogManager`, `GameProfile` made `partial` with
  `GameProfile.CreateHeadless` / `GetSplit`, and `ImageCache` returning null for missing theme images.
- **New code** in `src/Nucleus.Gaming/Headless`: `EngineHost` (init, default Settings.ini, handler loading),
  `HeadlessDevices` (screens, XInput, DirectInput, raw keyboards/mice and the `PlayerInfo` factories),
  `HeadlessOptions`, `HeadlessSession` (start, stop, ended), `GameLinks` (links without elevation, safe cleanup).
- **No UAC for hard links:** `StartGameUtil.SymlinkGame` starts `StartGame.exe` with `runas` only when symbolic links
  are made (`SymlinkGame`, or `HardlinkGame` with `SymlinkFolders`). Hard links to files need no elevation, so
  `HardlinkGame` handlers no longer show a UAC prompt.
- **No UAC at all for linked games (2026-11-07):** `StartGameUtil.SymlinkGame` now links in-process (the same
  `WinDirectoryUtil.LinkDirectory` walk StartGame.exe does) and never elevates, unless `GameLinks.AllowElevation`
  (job `"allowElevation": true`) asks for the upstream elevated StartGame.exe. Every link goes through the new
  `Headless/GameLinks`: an unprivileged symbolic link (`SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE`, Developer
  Mode) first; when Windows refuses it, a directory junction for folders and a hard link (same volume) or a copy for
  files. `HardlinkGame` falls back to copies too. `WinDirectoryUtil` no longer links files "into" a folder it just
  made a link (Nucleus did; every such call failed or tried to copy a file onto itself). `SymlinkFoldersTo` /
  `HardlinkFoldersTo` use the same helpers. The methods used are logged (`[LINK]`). Test switch:
  `PLAYOUTCOOP_NO_SYMLINKS=1` behaves as if symbolic links were not allowed.
- **Safe content cleanup (2026-11-07):** `CleanGameContent` deletes the content folder with `GameLinks.DeleteTree`,
  which never enters a symbolic link or junction (it removes the link itself) and deletes read-only hard links without
  clearing the read-only attribute of the original. Upstream's fallback enumerated through links, reset attributes and
  recursively deleted folders under them, which could reach the original game folder.
- **Original attributes restored (2026-11-07):** `StartGameUtil.UnlockGameFiles` (Nucleus resets every original game
  file to `Normal`) remembers the old attributes; `RestoreGameFileAttributes()` puts them back when a headless session
  ends.
- **Configurable content folder (2026-11-07):** `GameManager.ContentRootOverride` / `GetGameContentPath(guid)`; every
  use of `content\<GUID>` (instance folders, backups, BackupDatas, SDL2/DInput8 tools, cleanup) follows it (job
  `"contentRoot"`).
- **User stops (2026-11-07):** the Ctrl+E stop hotkey and the gamepad stop shortcut call
  `HeadlessSession.NoteUserStop`, so the session ends as `stopped` instead of `exited`.
- **Engine state stays in the engine root:** when headless, `GameMetaInfo` keeps `metaInfo.json` in the engine root
  instead of `%USERPROFILE%\NucleusCoop` (shared with an installed Nucleus Co-op).
- **DPI values restored:** `GameDpiAwareness` remembers the `HKCU\…\AppCompatFlags\Layers` values it sets;
  `RestoreAll()` (called when a headless session ends) deletes the new ones and puts back the old data of the others.
- **No blocking dialog from StartGame.exe:** a failed launch is printed on stdout instead of a `MessageBox` (which
  blocked the engine, waiting for StartGame.exe to exit). `StartGameUtil.StartGame` no longer reuses the previous
  instance's PID line and fails with a clear message when StartGame.exe is missing or printed no PID.
- **New program** `src/PlayOutCoop`: `PlayOutCoop.exe`, the command-line helper implementing
  [CONTRACT.md](CONTRACT.md) (`devices`, `inspect`, `play`).

## PlayOutCoop.exe

```
PlayOutCoop.exe devices --root <engineRoot>
PlayOutCoop.exe inspect --root <engineRoot> --handler <engineRoot>\handlers\<GameName>.js
PlayOutCoop.exe play    --root <engineRoot> --job <job.json> [--prompts auto|native] [--parent <pid>]
```

One JSON object per line on stdout (UTF-8, no BOM); diagnostics on stderr and in `<engineRoot>\logs\`. During
`play`, write `stop` on stdin to end the session; closing stdin ends it too, and so does the `--parent` process
exiting. With `--prompts auto` (default) `play` reports every Nucleus dialog as `{"type":"ui","kind":...,"answer":...}`
(auto-answered with its default); with `--prompts native` the dialogs that wait for the player are shown as real
top-most dialogs (`prompt` / `promptAnswered` events, see CONTRACT.md). OSD notices come as
`{"type":"status","osd":true,...}`. A `play` that fails before launching prints `error` and then
`{"type":"ended","reason":"error"}`. Before launching, `play` checks that the engine root holds the Nucleus files the
handler needs (StartGame.exe, the injectors and hook DLLs, EasyHook, ProtoInput when the handler uses it, the
`utils\` tool folders it enables) and fails with an `error` naming the missing ones. Exit codes: 0 success, 1 failure,
2 bad command line.

## Build

Requirements: the .NET 8 SDK (or newer) on Windows. No Visual Studio or C++ toolchain is needed.

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

This runs `dotnet build PlayOutCoop.sln -c Release` and copies everything PlayOut ships into `dist\`:
`PlayOutCoop.exe`, `Nucleus.Gaming.dll`, Jint, the other managed dependencies, `Nucleus.IJx86.exe`,
`Nucleus.IJx64.exe`, `StartGame.exe`, `EasyHook.dll`, `LICENSE`, `README.md` and `CONTRACT.md`. The test projects build
into `bin\test\Release\` and are not shipped.
