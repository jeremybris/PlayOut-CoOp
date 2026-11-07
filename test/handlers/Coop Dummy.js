// Test handler for CoopDummy.exe (PlayOut Co-Op test game). Nucleus handler format.
Hub.Handler.Version = 1;
Hub.Handler.Id = "coopdummy-test";
Game.GameName = "Coop Dummy";
Game.ExecutableName = "CoopDummy.exe";
Game.GUID = "CoopDummy";
Game.MaxPlayers = 4;
Game.MaxPlayersOneMonitor = 4;
Game.Description = "PlayOut Co-Op test game.";
Game.HardcopyGame = true;
Game.SupportsKeyboard = true;
Game.Hook.XInputEnabled = true;
Game.Hook.CustomDllEnabled = true;
Game.SupportsPositioning = true;
Game.HandlerInterval = 100;
Game.PauseBetweenStarts = 1;
Game.KillMutex = ["CoopDummySingleInstance"];
Game.AddOption("Mode", "Test option", "mode", ["alpha", "beta"]);
Game.Play = function() {
    Context.StartArguments = "--player " + Context.PlayerID + " --mode " + Context.Options["mode"] + " --gamepad " + Context.GamepadId;
};
