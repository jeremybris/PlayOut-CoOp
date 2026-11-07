// Test handler for CoopDummy.exe (PlayOut Co-Op test game) using SymlinkGame, as most hub handlers do: the instance
// folders hold links to the game's files. PlayOutCoop.exe makes them without elevation (unprivileged symbolic links,
// else hard links or copies). SymlinkFolders off (Nucleus's default): folders are real, files are linked.
Hub.Handler.Version = 1;
Hub.Handler.Id = "coopdummy-symlink-test";
Game.GameName = "Coop Dummy Symlink";
Game.GUID = "CoopDummySymlink";
Game.Description = "PlayOut Co-Op test game (symlinked instances).";
Game.SymlinkGame = true;
Game.SymlinkFolders = false;
Game.FileSymlinkCopyInstead = ["config.ini"];
Game.ExecutableName = "CoopDummy.exe";
Game.MaxPlayers = 4;
Game.MaxPlayersOneMonitor = 4;
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