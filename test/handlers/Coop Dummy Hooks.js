// Test handler for CoopDummy.exe exercising Nucleus's hook path:
// StartGame.exe + Nucleus.IJx86.exe tier 0 (Nucleus.SHook32.dll at startup) and tier 1 (Nucleus.Hook32.dll at runtime).
Hub.Handler.Version = 1;
Hub.Handler.Id = "coopdummy-hooks-test";
Game.GameName = "Coop Dummy Hooks";
Game.ExecutableName = "CoopDummy.exe";
Game.GUID = "CoopDummyHooks";
Game.MaxPlayers = 4;
Game.MaxPlayersOneMonitor = 4;
Game.HardcopyGame = true;
Game.SupportsKeyboard = true;
Game.Hook.XInputEnabled = true;
Game.Hook.CustomDllEnabled = true;
Game.SupportsPositioning = true;
Game.HandlerInterval = 100;
Game.PauseBetweenStarts = 2;
Game.SetWindowHookStart = true;
Game.HookFocus = true;
Game.SetWindowHook = true;
Game.Play = function() {
    Context.StartArguments = "--player " + Context.PlayerID;
};
