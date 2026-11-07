# PlayOut Co-Op helper — interface contract (v1)

PlayOut (Electron, closed source, MIT) drives a separate GPL-3 helper program, **PlayOutCoop.exe**, built from
Nucleus Co-op's engine (SplitScreen-Me/splitscreenme-nucleus, GPL-3). The two talk only through this contract:
command-line arguments, a JSON job file, JSON lines on stdout, and plain commands on stdin. Keep it coarse.

## Engine root

All engine state lives in one writable folder, the **engine root**, which PlayOut prepares (under its userData,
e.g. `%APPDATA%\PlayOut\coop\engine`). Nucleus's `Globals.NucleusInstallRoot` must point there, not at the exe's
folder (the exe ships read-only inside PlayOut's install: `resources\coop\`). Layout:

```
<engineRoot>\
  handlers\<GameName>.js           extracted handler scripts (PlayOut extracts them, Nucleus's naming)
  handlers\<GameName>\...          the handler's extra files
  utils\...                        Nucleus's third-party tools (PlayOut extracts from the official NucleusApp.zip)
  Nucleus.Hook32.dll, Nucleus.Hook64.dll, Nucleus.SHook32.dll, Nucleus.SHook64.dll,
  EasyHook*.dll/exe, ProtoInput*.dll/exe, openxinput1_3.dll, SDL2.dll   (from NucleusApp.zip, root level)
  content\<GUID>\Instance<N>\      instance folders (the engine creates and cleans them)
  Settings.ini                     the engine's own settings (the helper writes sane defaults if missing)
  logs\                            helper logs
```

The helper's own binaries (PlayOutCoop.exe, its engine DLL, Jint etc., the injector exes it builds from Nucleus
source such as Nucleus.IJx86.exe / Nucleus.IJx64.exe and StartGame.exe) are shipped by PlayOut in
`resources\coop\` and may be copied by PlayOut into the engine root before use if the engine needs them next to the
hook DLLs. The helper must work when run from `<engineRoot>` with all of these side by side (PlayOut will copy them
there: simplest and matches Nucleus's assumptions).

## Commands

Every command prints exactly **one JSON object per line** on stdout (UTF-8, no BOM). Diagnostics go to stderr and to
`<engineRoot>\logs\`. Exit code 0 = success, non-zero = failure (also print `{"type":"error","message":...}`).

### `PlayOutCoop.exe devices --root <engineRoot>`
One line:
```json
{"type":"devices",
 "gamepads":[{"id":"xinput:0","kind":"xinput","index":0,"name":"Xbox Controller","guid":"...", "connected":true},
             {"id":"dinput:{guid}","kind":"dinput","index":-1,"name":"Wireless Controller","guid":"{instance guid}"}],
 "keyboards":[{"id":"kb:<handle>","handle":"0x...","name":"..."}],
 "mice":[{"id":"mouse:<handle>","handle":"0x...","name":"..."}],
 "screens":[{"index":0,"primary":true,"bounds":{"x":0,"y":0,"width":2560,"height":1440},"name":"\\\\.\\DISPLAY1"}]}
```
XInput slots 0-3 (more via openxinput if available); DirectInput devices that are not XInput (skip `IG_` paths).
Must not require a handler.

### `PlayOutCoop.exe inspect --root <engineRoot> --handler <path\to\GameName.js>`
Runs the handler script (as Nucleus does, Jint) to read its fields — does NOT launch anything. One line:
```json
{"type":"handler","gameName":"...","guid":"...","exeName":"x.exe","exeNames":["..."],"steamId":"...",
 "maxPlayers":4,"maxPlayersOneMonitor":4,"supportsKeyboard":true,"supportsMultipleKeyboardsAndMice":false,
 "needsSteamClient":false,"binariesFolder":"...","hubId":"...","hubVersion":13,
 "usesXInput":true,"usesDInput":false,
 "options":[{"key":"...","name":"...","description":"...","values":["..."],"default":"..."}],
 "description":"..."}
```

### `PlayOutCoop.exe play --root <engineRoot> --job <path\to\job.json>`
Runs one co-op session with Nucleus's GenericGameHandler, headless (no Nucleus UI; any Nucleus dialog/prompt that
would block must be auto-answered or reported as an event). The helper stays running for the whole session.

job.json:
```json
{
  "handler": "C:\\...\\engine\\handlers\\Borderlands 2.js",
  "exePath": "D:\\Steam\\steamapps\\common\\Borderlands 2\\Binaries\\Win32\\Borderlands2.exe",
  "screens": [ {"index":0, "layout":"DualHorizontal"} ],
  "players": [
    {"nickname":"Player1", "screen":0, "slot":0, "device":{"kind":"xinput","index":0}},
    {"nickname":"Player2", "screen":0, "slot":1, "device":{"kind":"keyboard","keyboardHandle":"0x...","mouseHandle":"0x..."}}
  ],
  "options": {"<handler option key>": "<value>"}
}
```
- `layout` is a Nucleus `UserScreenType` name: `FullScreen`, `DualHorizontal` (top/bottom), `DualVertical`
  (side by side), `FourPlayers`, `SixPlayers`, `EightPlayers`, `SixteenPlayers`. Bounds come from Nucleus's own
  `UserScreen` split math on that monitor (`slot` = index into the split, row-major).
- `device.kind`: `xinput` (index = slot), `dinput` (guid), `keyboard` (raw handles; "0x0" for the shared
  keyboard/mouse), or `none`.
- Player order = instance order (PlayerID).

stdout events (one per line), at least:
```json
{"type":"status","message":"Starting instance 1 of 2"}
{"type":"progress","step":"instance","player":0,"total":2}
{"type":"started","pids":[1234,5678]}
{"type":"error","message":"...", "fatal":true}
{"type":"ended","reason":"exited|stopped|error"}
```
stdin commands: `stop` (end the session: Nucleus `End()`, restore everything, close instances), then the helper exits
after printing `ended`. When all game processes exit by themselves the helper also cleans up, prints `ended` and
exits. If PlayOut dies (stdin closes), the helper ends the session the same way.

## Out of scope for the helper
Handler download/indexing/extraction, tool download, choosing players/devices/layout automatically, UI: all PlayOut.
