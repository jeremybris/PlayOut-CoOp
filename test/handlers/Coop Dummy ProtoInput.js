// Test handler for CoopDummy.exe using ProtoInput the way most current hub handlers do (runtime injection through
// EasyHook, the XInput hook, a message filter): the case of "Star Wars Battlefront: Classic Collection", which failed
// with EntryPointNotFoundException when the installed ProtoInputLoader lacked the newer loader functions.
Hub.Handler.Version = 1;
Hub.Handler.Id = "coopdummy-protoinput-test";
Game.GameName = "Coop Dummy ProtoInput";
Game.ExecutableName = "CoopDummy.exe";
Game.GUID = "CoopDummyProtoInput";
Game.MaxPlayers = 4;
Game.MaxPlayersOneMonitor = 4;
Game.Description = "PlayOut Co-Op test game (ProtoInput).";
Game.SymlinkGame = true;
Game.SupportsKeyboard = false;
Game.SupportsMultipleKeyboardsAndMice = true;
Game.Hook.XInputEnabled = false;
Game.SetWindowHook = true;
Game.SupportsPositioning = true;
Game.HandlerInterval = 100;
Game.PauseBetweenStarts = 1;
Game.KillMutex = ["CoopDummySingleInstance"];

Game.ProtoInput.InjectStartup = false;
Game.ProtoInput.InjectRuntime_RemoteLoadMethod = false;
Game.ProtoInput.InjectRuntime_EasyHookMethod = true;
Game.ProtoInput.InjectRuntime_EasyHookStealthMethod = false;
Game.ProtoInput.MessageFilterHook = true;
Game.ProtoInput.ClipCursorHook = true;
Game.ProtoInput.XinputHook = true;
Game.ProtoInput.UseOpenXinput = true;
Game.ProtoInput.BlockedMessages = [0x0008];
Game.LockInputAtStart = false;

Game.Play = function() {
    Context.StartArguments = "--player " + Context.PlayerID + " --gamepad " + Context.GamepadId;
};
