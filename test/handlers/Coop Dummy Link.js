// Test handler for CoopDummy.exe (PlayOut Co-Op test game) using HardlinkGame: instance folders are hard links to the
// game's files (no copy, no elevation). Used by the PlayOutCoop.exe "play" test.
Hub.Handler.Version = 1;
Hub.Handler.Id = "coopdummy-link-test";
Game.GameName = "Coop Dummy Link";
Game.ExecutableName = "CoopDummy.exe";
Game.GUID = "CoopDummyLink";
Game.MaxPlayers = 4;
Game.MaxPlayersOneMonitor = 4;
Game.Description = "PlayOut Co-Op test game (hard-linked instances).";
Game.HardlinkGame = true;
Game.SymlinkFolders = false;
Game.SupportsKeyboard = true;
Game.Hook.XInputEnabled = true;
Game.Hook.CustomDllEnabled = true;
Game.SupportsPositioning = true;
Game.HandlerInterval = 100;
Game.PauseBetweenStarts = 1;
Game.AddOption("Mode", "Test option", "mode", ["alpha", "beta"]);
Game.Play = function() {
    Context.StartArguments = "--player " + Context.PlayerID + " --mode " + Context.Options["mode"] + " --gamepad " + Context.GamepadId;
};
