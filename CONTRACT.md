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
  content\<GUID>\Instance<N>\      instance folders (the engine creates and cleans them; <contentRoot>\<GUID>\... when
                                   the job sets "contentRoot")
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

### `PlayOutCoop.exe play --root <engineRoot> --job <path\to\job.json> [--prompts auto|native] [--parent <pid>]`
Runs one co-op session with Nucleus's GenericGameHandler, headless (no Nucleus UI; any Nucleus dialog/prompt that
would block must be auto-answered or reported as an event). The helper stays running for the whole session.

Options:
- `--prompts auto` (default): every Nucleus dialog is answered at once with its default button and reported as a
  `ui` event (see below). Nothing waits for the player.
- `--prompts native` (PlayOut passes this): the requests that must wait for the player are shown as real Windows
  dialogs, top-most and not owned by any PlayOut or game window, and the engine waits for the answer, as Nucleus did
  with its own dialogs. These are: the "Press OK when ..." pauses (`ok`), Yes/No (and other multi-button) questions
  (`yesno`), handler text prompts (`text`, `Game.CustomUser*Prompts`) and the launcher-exe file picker (`file`,
  `Game.LauncherExe = "NucleusDefined"`; an open-file dialog owned by an invisible top-most helper window so that it
  stays on top). Purely informational message boxes, Nucleus message boxes, OSD notices and the process picker stay
  auto-answered (`ui` / `status` events). Each shown dialog is reported as `prompt`, then `promptAnswered`. A `stop`,
  a closed stdin or the parent exiting while a dialog is open closes it (answered as cancelled/default) and every
  later request is auto-answered while the session ends.
- `--parent <pid>`: the helper watches that process (PlayOut's main process). When it exits (or does not exist when
  the helper starts), the session ends exactly like when stdin closes: Nucleus `End()`, restore, `ended`
  (`"reason":"stopped"`), exit. A PID that belongs to a process started after the helper is treated as already gone.

Bad option values exit with code 2 after an `error` event.

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
  "options": {"<handler option key>": "<value>"},
  "contentRoot": "D:\\PlayOutCoop",
  "allowElevation": false
}
```
- `contentRoot` (optional, absolute folder, e.g. on the same drive as the game): the instance folders are created
  as `<contentRoot>\<GUID>\Instance<N>` instead of `<engineRoot>\content\<GUID>\Instance<N>`. Created if missing.
  Put it on the game's volume so hard links (below) work when symbolic links are not allowed. At the end of the
  session the helper deletes `<contentRoot>\<GUID>` (the engine's own folder for that handler, as Nucleus does for
  `content\<GUID>`); the contentRoot folder itself and anything else in it are left alone. It must not be inside the
  game folder (nor the game inside `<contentRoot>\<GUID>`): that fails with an `error` before anything is launched.
- `allowElevation` (optional, default false): only when true do handlers that link game files (`Game.SymlinkGame`,
  or `HardlinkGame` with `SymlinkFolders`) use Nucleus's elevated `StartGame.exe` (`runas`, a UAC prompt) as upstream
  Nucleus does. PlayOut leaves it false.
- **Linking without UAC (default):** the helper links the game into the instance folders in its own process, never
  elevated, keeping Nucleus's rules (which files are copied — the exe unless `SymlinkExe`, `FileSymlinkCopyInstead`,
  `DirSymlinkCopyInstead` — which are excluded, and `SymlinkFolders`). Per item it first makes an unprivileged
  symbolic link (works with Windows Developer Mode on); when Windows refuses that, folders become NTFS directory
  junctions and files become hard links (same volume) or plain copies (other volume, FAT/exFAT). `HardlinkGame`
  makes hard links, falling back to copies. When neither a symbolic link nor a hard link is possible, the helper
  measures what would be copied for all players first and, if that would leave less than 2 GB free on that drive,
  fails with an `error` (`ended` `"reason":"error"`) before copying anything; the message says to turn on Developer
  Mode or set `contentRoot` on the game's NTFS drive. The log (`[LINK]` lines) says which methods were used. Cleanup never
  follows a link or junction: it removes the link itself, so the original game files are never deleted or changed.
  Nucleus clears the attributes (read-only etc.) of the original game files before linking; the helper puts them
  back when the session ends.
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
Also:
```json
{"type":"ui","kind":"MessageBox|NucleusMessageBox|Prompt|SelectFile|CustomPrompt|ProcessPick","title":"...","message":"...","icon":"...","answer":"OK|Yes|Cancel|continue|...","source":"File.cs:Member:line"}
{"type":"prompt","kind":"ok|yesno|text|file","title":"...","message":"...","buttons":"YesNo|OKCancel|...|null","source":"File.cs:Member:line"}
{"type":"promptAnswered","kind":"ok|yesno|text|file","answer":"ok|yes|no|cancel|...","cancelled":false}
```
- `ui`: a dialog the helper answered by itself with its default (every dialog with `--prompts auto`; the
  informational ones with `--prompts native`).
- `prompt` (only with `--prompts native`): a dialog is now on screen and the launch waits for the player. `buttons`
  is set for `yesno` (the Windows button set). Followed by exactly one `promptAnswered` when it is closed: `answer` is
  the button (`ok`, `yes`, `no`, `cancel`, ...; for `file`/`text` `ok` means a file was chosen / text entered, the
  value itself is not reported); `cancelled` is true when the helper closed it because the session is ending.
- `status` with `"osd":true`: Nucleus's on-screen notices.

stdin commands: `stop` (end the session: Nucleus `End()`, restore everything, close instances), then the helper exits
after printing `ended`. When all game processes exit by themselves the helper also cleans up, prints `ended` and
exits. If PlayOut dies (stdin closes, or `--parent` exits), the helper ends the session the same way.

`ended.reason`: `stopped` = ended on request (`stop`, stdin closed, `--parent` exited, Nucleus's Ctrl+E stop hotkey
or its gamepad stop shortcut, or a stop before launch); `exited` = every game process exited by itself; `error` =
the launch failed or was aborted (preceded by an `error` event).

## Out of scope for the helper
Handler download/indexing/extraction, tool download, choosing players/devices/layout automatically, UI: all PlayOut.

## Changes
- 2026-11-07: `play` options `--prompts auto|native` and `--parent <pid>`; job fields `contentRoot` and
  `allowElevation`; events `prompt` / `promptAnswered` (and the existing `ui` event documented); game files are linked
  without elevation (no UAC) unless `allowElevation`; the Ctrl+E hotkey / gamepad stop now ends with
  `"reason":"stopped"` (was `exited`).
- 2026-11-07 (later): when game files can only be copied (no symbolic or hard links), a copy that would leave less than
  2 GB free fails with an `error` before anything is copied.
