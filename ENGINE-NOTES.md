# Engine notes for the PlayOutCoop.exe CLI

How to drive the ported Nucleus engine (`Nucleus.Gaming.dll`) from the CLI described in [CONTRACT.md](CONTRACT.md).
`test/EngineSmoke/Program.cs` is a working example of every step below (devices, inspect, split, play, stop).

## Process setup

- **Build the CLI 32-bit:** `PlatformTarget AnyCPU` + `Prefer32Bit true` (or x86), net48, the same as `NucleusCoop.exe`.
  Nucleus assumes a 32-bit host in places: `RawInputManager.GetDeviceList` uses `IntPtr.ToInt32()`, and ProtoInput
  picks `ProtoInputLoader32/64.dll` by `IntPtr.Size`. The injectors handle 64-bit games themselves.
- Reference `src/Nucleus.Gaming/Nucleus.Gaming.csproj`. Keep the SDK-generated `.exe.config` (binding redirects for
  System.Memory, System.Resources.Extensions, etc.).
- **Keep stdout clean:** Nucleus writes to `Console.Out` in about 40 places (LogManager, StartGame output relays…). Open
  `Console.OpenStandardOutput()` for the JSON lines first, then `Console.SetOut(Console.Error)`.
- **Run from the engine root** with everything side by side (contract): our `Nucleus.Gaming.dll`, `Jint.dll`,
  `Nucleus.IJx86/x64.exe`, `StartGame.exe`, `WindowScrape.dll`, SharpDX, etc., plus the native files PlayOut extracts.
  `EngineHost.Initialize` also makes the root the current directory: Nucleus writes `debug-log*.txt` there, and
  `Nucleus.IJ*.exe` reads `Settings.ini` from the current directory.
- **Exit with `Environment.Exit(code)`** after printing `ended`. Nucleus leaves foreground threads running
  (`Dispatcher.Run()` on the play thread and the gameplay-timer thread), so returning from `Main` does not end
  the process.
- `[STAThread]` on `Main` is fine but not required. The engine creates its own STA threads.

## 0. Initialise

```csharp
EngineUI.LogSink = (category, msg) => log.WriteLine($"[{category}] {msg}");   // optional, before Initialize
EngineHost.Initialize(engineRoot);  // or: new EngineHostOptions { AllowBackgroundWindows = false, ... }
```

`Initialize`:
- sets `Globals.NucleusInstallRoot` = engineRoot and the current directory;
- creates `handlers\`, `content\` and `utils\backup\`;
- writes Nucleus's default `Settings.ini` if missing, with `[Misc] DisableGameProfiles=True` (so no
  `game profiles\` JSON is saved per launch) and `IgnoreInputLockReminder=True`;
- sets `EngineUI.Headless = true`;
- sets DPI awareness as `NucleusCoop.exe` does (`DPIManager.PreInitialize()`, then
  `User32Util.SetProcessDpiAwareness`, which always selects per-monitor aware). All bounds are physical pixels;
- loads the settings (`App_Settings_Loader`) and `PlayersIdentityCache`;
- creates `LogManager` (`content\app.log`);
- creates `new GameManager(false)`. That registers the raw-input sink window, does not scan `handlers\`, and
  loads `content\userprofile.json`.

Call it once per process, before anything else in the engine.

## 1. Load a handler into a GenericGameInfo

```csharp
GenericGameInfo g = EngineHost.LoadHandler(@"<root>\handlers\Borderlands 2.js");
```

This runs the script with Jint 2.11.58 exactly like Nucleus (`new GenericGameInfo(fileName, handlers\<name>, stream, …)`;
the CLR is open to the script; `handlers\<name>\pluginsInfo.txt` plugins are loaded). On a script error it throws
`InvalidOperationException("Handler X.js failed: Line N: …")`. Line numbers are halved like Nucleus does, because
Nucleus pads each line. The handler must live in `<root>\handlers\`: Nucleus resolves `JsFileName`, the handler's
folder and custom prompt value files against `GameManager.GetJsScriptsPath()`.

Fields for `inspect`: `g.GameName`, `g.GUID`, `g.ExecutableName`, `g.ExecutableNames`, `g.SteamID`, `g.MaxPlayers`,
`g.MaxPlayersOneMonitor`, `g.SupportsKeyboard`, `g.SupportsMultipleKeyboardsAndMice`, `g.NeedSteamClient`,
`g.BinariesFolder`, `g.Hub.Handler.Id`, `g.Hub.Handler.Version`, `g.Description`.
- `HeadlessDevices.DeviceKindsFor(g, out xinput, out dinput, out sdl2, out keyboard, out raw)` gives the same device
  families DevicesFunctions would offer (`usesXInput` / `usesDInput`).
- `g.Options.Select(HeadlessOptions.Describe)` gives `{Key, Name, Description, Values, Default, Type, Hidden}`.
  `Default` is what Nucleus really uses: `List[0]` for list options, because the options page ignores
  `GameOption.DefaultValue`.

`inspect` launches nothing, but loading a handler runs its top-level script. That script is unsandboxed .NET code,
the same as in Nucleus.

## 2. Enumerate devices (no handler needed)

| Contract field | Call | Notes |
|---|---|---|
| `screens` | `HeadlessDevices.GetScreens()` | `User32Util.GetDisplays()` order. `Index` = the screen index used everywhere else. `Primary` is computed (Nucleus's own flag is always true). |
| `gamepads` xinput | `HeadlessDevices.GetXInputPads(4 or 32)` | Uses `openxinput1_3.dll` from the root (Nucleus's way, up to 32 slots). Falls back to the system `xinput1_4.dll` (4 slots) if it is missing. `VendorId`/`ProductId` come from `XInputGetCapabilitiesEx`. |
| `gamepads` dinput | `HeadlessDevices.GetDInputPads()` | SharpDX `GetDevices(GameControl, AttachedOnly)`, same order as `DInputManager`. `Index` is Nucleus's `GamepadId` for DInput players. Skip `IsXInputDevice` (`IG_` in the path) as the contract says. |
| `keyboards` / `mice` | `HeadlessDevices.GetRawKeyboardsAndMice()` | `RawInputManager.GetDeviceList()` (dwType 1 = keyboard, 0 = mouse). `Handle` is the raw device handle; print it as `"0x" + handle.ToString("X")`. |

SDL2 controllers (`Hook.SDL2Enabled`) are not enumerated yet (see gaps).

## 3. Build the GameProfile (players, screens, split bounds)

```csharp
UserGameInfo ug = EngineHost.CreateUserGame(g, job.exePath);       // UserGameInfo.InitializeDefault, not saved

UserScreen[] screens = HeadlessDevices.GetUserScreens();           // ScreensUtil.AllScreens(), Index set
screens[0].Type = UserScreenType.DualHorizontal;                   // contract "layout" names = enum names
List<Rectangle> split = GameProfile.GetSplit(screens[0]);          // Nucleus's UserScreen.UpdateSubBounds, row-major

var slots = new List<HeadlessPlayerSlot> {
    new HeadlessPlayerSlot { Player = HeadlessDevices.CreateXInputPlayer(0), ScreenIndex = 0, Slot = 0, Nickname = "Player1" },
    new HeadlessPlayerSlot { Player = HeadlessDevices.CreateSharedKeyboardPlayer(), ScreenIndex = 0, Slot = 1 },
};
GameProfile profile = GameProfile.CreateHeadless(ug, screens, slots, job.options /* IDictionary<string,string> */);
```

- **Split math:** `DualHorizontal` = top/bottom, `DualVertical` = side by side. `FourPlayers` is 2×2,
  `SixPlayers` 2×3, `EightPlayers` 2×4, `SixteenPlayers` 4×4. Set `Type` after `GetUserScreens()`: setting it
  recomputes `SubScreensBounds`. Verified on a 1920×1200 screen: DualHorizontal gives (0,0,1920,600) and
  (0,600,1920,600); DualVertical gives (0,0,960,1200) and (960,0,960,1200); FourPlayers gives four 960×600 slots.
- **Device → PlayerInfo** (same fields `DevicesFunctions` / `RawInputManager` set):

  | Job device | Factory |
  |---|---|
  | `xinput` index n | `CreateXInputPlayer(n)` (`GamepadId = n`; GUID `…2000000000NN` when `GameProfile.UseXinputIndex`) |
  | `dinput` guid | `CreateDInputPlayer(guid)` (opens and acquires the device; `GamepadId` = DInput enumeration index; returns null if the device is gone) |
  | `keyboard` with both handles `0x0` (or missing) | `CreateSharedKeyboardPlayer()` (Nucleus's single KB/M player, `GamepadId 99`) |
  | `keyboard` with raw handles | `CreateRawKeyboardMousePlayer(kb, mouse)`: the merged raw KB+M player. Needs `SupportsMultipleKeyboardsAndMice` or ProtoInput. Pass `(IntPtr)(-1)` for "no device". |
  | `none` | `CreateNoInputPlayer()` |

- `CreateHeadless` copies the setup screen's steps:
  1. sets `GameProfile.Game`, `GameInfo` and `Instance`, then runs `InitializeDefault()`;
  2. runs `BoundsFunctions.AddPlayer` logic for each slot (`Owner`, `ScreenIndex`, `MonitorBounds`, `EditBounds`,
     `DisplayIndex`, `AssignedDevices`);
  3. calls `UpdateProfilePlayerIdentity` (nickname and Steam ID from Settings.ini or the handler), then applies
     `Nickname` / `SteamID` overrides;
  4. resolves options with `HeadlessOptions.Resolve`. The value types match what the options page stores: the
     list item object, the enum, bool, decimal, or string. A list value that matches no item throws `ArgumentException`;
  5. returns `CleanClone(profile)`, which is what Nucleus passes to the handler.

  Player order = list order = `PlayerID`, which is the instance order. Bad screen or slot indexes and
  double-booked slots throw `ArgumentException`.

## 4. Start Play

```csharp
EngineUI.ProgressSink = (step, i, total) => Emit(progress);       // "instance", 0-based player, total
EngineUI.Handler = req => { Emit(uiEvent(req)); return null; };   // null = default answer (see below)

HeadlessSession session = HeadlessSession.Start(ug, profile);     // returns immediately
session.PlayFinished += err => { if (string.IsNullOrEmpty(err) && !session.HasEnded) Emit(started(session.Pids)); else ... };
session.Ended += reason => { Emit(ended(reason, session.Error)); Environment.Exit(...); };
```

- `Start` does what the Play button does: `GameManager.MakeHandler`, then `GenericGameHandler.Initialize`. That starts
  Nucleus's STA play thread (`Play()`) and the window update tick (`HandlerInterval`), plus Nucleus's global hotkey
  listener: Ctrl+E stops, Ctrl+Q and the rest work as in Nucleus.
- `PlayFinished(error)` fires on the play thread when `Play()` returns. That means every instance has been started
  and found. Windows are then resized, moved and stripped of borders by the tick thread over the next few hundred ms.
  If Nucleus aborted the launch itself (for example "file path too long" in `UnlockGameFiles`), `End()` has already
  run, so check `session.HasEnded` before reporting `started`.
- `session.Pids` = `GenericGameHandler.attachedIds`, in instance order.
- `Ended(reason)` fires after `End()` has restored everything:
  - `"stopped"`: `Stop()` was called;
  - `"error"`: `Play()` threw or returned an error, or the launch was aborted before `Play()` returned.
    `session.Error` holds the message, or `EngineUI.LastMessage` (the last error/warning dialog text);
  - `"exited"`: every game process exited by itself (Nucleus's `exited == players.Count` check), or the user
    pressed Nucleus's Ctrl+E hotkey.
- **Prompts that block:** with the default answers, every `Prompt` continues at once. A handler that sets
  `PromptBetweenInstances`, `PromptAfterFirstInstance` or `ThirdPartyLaunch`, or that has a `Context.RunAdditionalFiles`
  prompt, expects the user to do something first. The CLI can make `EngineUI.Handler` block for `Kind == Prompt` until
  PlayOut answers (for example a `continue` stdin command). Blocking pauses the launch the way the modal dialog did.

## 5. Stop / End()

```csharp
session.Stop(timeoutMs: 60000);   // = Stop button: unlocks input (End key) if locked, then GenericGameHandler.End(true)
```

`End()` closes the overlay windows, runs `Game.OnStop`, kills the instances (`KillRemainingProcess`), restores
backed-up files and the registry, resets DPI scaling and the taskbar, deletes temporary users and adapters, and in
Release deletes `content\<GUID>` unless `KeepSymLinkOnExit`. `Stop()` runs `End` on a new STA thread and waits for
`Ended`. Call it on `stop`, and also when stdin closes (PlayOut died). Then print `ended` and `Environment.Exit`.

## Threading

| Thread | Created by | Runs |
|---|---|---|
| Caller (CLI main) | you | `EngineHost`, `LoadHandler`, device enumeration, `CreateHeadless`, `HeadlessSession.Start/Stop`, waiting |
| Play thread (STA, foreground, `Dispatcher.Run` afterwards) | `GenericGameHandler.Initialize` | `Play()`, `Game.Play` JS, most `EngineUI` requests, `PlayFinished` |
| Update tick (foreground) | `StartUpdateTick` | window resize/position/border passes, exit detection → `End(false)` → `Ended` |
| Hotkey listener (STA, background) | `HotkeyListenerThread` | global hotkeys, may call `End(false)` |
| Raw input (background) | `GameManager` | raw-input sink window |
| Task pool | Nucleus `Task.Run` | some `NucleusMessageBox` requests |

- `EngineUI.Handler`, `LogSink`, `ProgressSink` and the session events can fire on any of these threads, sometimes
  concurrently. Make stdout writes thread-safe (one lock).
- There is one session per process: Nucleus keeps state in statics (`GenericGameHandler.Instance`, `GameProfile.*`,
  `GameManager.Instance`).
- No message loop is needed on the caller thread.

## Every UI call site and its headless default

Default = what pressing Enter on the original dialog would do: its default button, the pre-filled value, or the first
candidate. Every request reaches `EngineUI.Handler` first (with `Kind`, `Title`, `Message`, `Buttons`, `Icon`,
`DefaultResult`, `Source = "File.cs:Member:line"`). It is also logged under category `UI`.

| Kind | Call sites (`src/Nucleus.Gaming/…`) | Headless default |
|---|---|---|
| MessageBox, info/error (OK) | App_Settings_Loader (settings load failed); GameManager ×2 (handler error while scanning); GenericContext ×2 (zip missing); GenericGameHandler: Initialize exception, End-key reminder (suppressed by `IgnoreInputLockReminder=True`), IdInWindowTitle and ChangeWindowTitle window not found; HotkeysRegistration ×4; ProtoInputLauncher ×2 (startup/runtime inject failed); CleanGameContent "no data"; FlawlessWidescreen plugin missing; Network ×3 (incl. the ChangeIPPerInstance warning); SteamFunctions ×4 (steam_interfaces, lobby connect); UserDriveInfo ×4 (exFAT etc.); ProcessUtil (lobby connect) | OK, continue |
| MessageBox Yes/No | FileUtil, NucleusUsers ×2 ("delete file in config path?", only without `IgnoreDeleteFilesPrompt`); CleanGameContent ("delete content folder?", UI-only path); LogManager ("enable debug logging?" after a crash, Release) | Yes (default button) |
| NucleusMessageBox | GenericGameInfo (handler script error, via Task.Run); GameProfile ×2 (profile load); LogManager (crash report); CleanGameContent (locked files); StartGameUtil ×2 (**path too long → launch aborted**); SteamFunctions ×2 (Goldberg/CustomSteamApiDllPath warnings) | logged, continue; error text kept in `EngineUI.LastMessage` |
| Prompt ("Press OK when …") | GenericGameHandler ×10 (`ThirdPartyLaunch`, `PromptProcessChangesAtEnd`, `PromptAfterFirstInstance`, `PromptBetweenInstances`, `PromptBeforeProcessGrab`, post-hook prompt, `PromptBetweenInstancesEnd`); GenericContext ×10 (`RunAdditionalFiles` prompts); Network (IP revert); SteamFunctions (Goldberg lobby connect: "after you are hosting") | continue immediately |
| SelectFile | GenericGameHandler ×2 (`Game.LauncherExe = "NucleusDefined[|exe]"`: pick the launcher) | Cancel: no file chosen (the handler's LauncherExe becomes null). Return `Result = OK, Value = path` to choose one; Nucleus then rewrites the handler file as it does today. |
| CustomPrompt | CustomPromptRuntime ×3 (`Game.CustomUserGeneralPrompts` / `PlayerPrompts` / `InstancePrompts`) | the previous saved answer (or empty). Return `Value` to answer. `Detail` = General/Player/Instance, `Index` = prompt index. |
| ProcessPick | ProcessPicker (`ForceProcessPick`, or the game process was not found) | the first candidate: windowed processes whose name matches the exe or launcher, not already attached. Null when there is none, and the instance then fails as in Nucleus. |
| Osd | GameProfile ×5, GenericGameHandler ×2, GamepadNavigation ×3, GamepadShortcuts ×5, RawInputProcessor ×3, HotkeyListener ×9, WindowMerger, GlobalWindowMethods ×13 | logged (`OSD`) and passed to the Handler; never blocks |
| (not shown) | ReminderFormThread (shortcut reminder overlays) | skipped when headless |
| (optional) | WPFDivFormThread (black split-division / `HideDesktop` background) | shown; set `EngineHostOptions.AllowBackgroundWindows = false` to skip |

Still shown, because these are not Nucleus dialogs:
- the Windows UAC prompt when a handler uses `SymlinkGame` / `HardlinkGame` (`StartGame.exe` runs elevated with `runas`);
- the elevation needed by `LaunchAsDifferentUsers*` and `ChangeIPPerInstance*`;
- any `System.Windows.Forms.MessageBox` a handler script calls directly through Jint.

## Side effects to know about (all upstream Nucleus behaviour)

- `Play()` starts by killing every running process named like the game exe, launcher or `KillProcessesOnClose`
  (`ProcessUtil.KillRemainingProcess`).
- For each instance exe, `HKCU\…\AppCompatFlags\Layers` gets a `~ HIGHDPIAWARE` value (`GameDpiAwareness`).
- `%USERPROFILE%\NucleusCoop\metaInfo.json` gets a play-time entry per handler GUID. Per-player environments live
  in `%USERPROFILE%\NucleusCoop\<nickname>`.
- In the engine root: `debug-log.txt`, `debug-log-startup-hook.txt` (`DebugLog=True` in the default Settings.ini),
  `content\app.log`, `content\userprofile.json` and `content\<GUID>\Instance<N>` (deleted at End).

## NucleusApp.zip

- The release zip is **encrypted** (ZipCrypto). The password `nucleus` is published in the v2.4.2 release notes.
  PlayOut's extractor must pass it.
- Extract only the native files the contract lists, plus `utils\`. **Do not copy the release's `Nucleus.Gaming.dll`,
  `Jint.dll`, `Nucleus.IJ*.exe`, `StartGame.exe` or `WindowScrape.dll` over ours.** The release's
  `Nucleus.Gaming.dll` has the same strong-name identity (same `GamingKey.snk`) and would silently replace the
  headless engine.

## Verified (by running, Windows 11, one 1920×1200 screen, no gamepads attached)

- `devices`: screens; 4 XInput slots through `openxinput1_3.dll` (all empty); 5 raw keyboards and 2 raw mice;
  DirectInput enumeration (0 devices).
- `inspect` of the test handler: fields and options as above. A broken handler gives a clean error.
- `split` math for DualHorizontal, DualVertical and FourPlayers.
- 2- and 3-player `play` of `test/CoopDummy` with HardcopyGame. Checked:
  - instance folders created and copied;
  - custom `xinput1_3.dll` and `ncoop.ini` written per instance;
  - handler option passed into `Context.Options`;
  - `KillMutex` done by our `StartGame.exe`;
  - processes found, then windows moved, resized to the exact slot rects and borderless;
  - `started` with PIDs; `stop` ends everything and deletes `content\<GUID>`;
  - killing the games ends the session with `exited`.
- Hook path (`test/handlers/Coop Dummy Hooks.js`):
  - `SetWindowHookStart` → `StartGame.exe` → our `Nucleus.IJx86.exe` tier 0 → `Nucleus.SHook32.dll` installed;
  - `HookFocus` / `SetWindowHook` → our `Nucleus.IJx86.exe` tier 1 → `Nucleus.Hook32.dll` loaded in both instances
    (checked in the instances' module lists).

## Known gaps

- Not tested: real XInput/DInput pads (no hardware attached), ProtoInput, 64-bit games (`Nucleus.IJx64.exe` built
  but not run), Goldberg/SSE/emulator handlers, SymlinkGame/HardlinkGame (UAC), multiple monitors, and real handlers
  from the hub.
- SDL2 controllers: no enumeration or factory yet. `SDLManager` needs `SDL2.dll` and its init thread.
- The CLI cannot see raw keyboard/mouse names for the HandleZero pseudo devices; pass `0x0` handles for the
  shared keyboard.
- `Ended("exited")` also covers Nucleus's Ctrl+E hotkey stop.
- The Nucleus UI classes (`Controls/*`, `Forms/*`) are still compiled into `Nucleus.Gaming.dll`, unused when headless.
- The hub update check (`MetaInfo.CheckUpdate`) is never triggered: handlers load with `checkUpdate = {false, false}`.
