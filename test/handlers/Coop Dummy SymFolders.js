// Test handler for CoopDummy.exe using SymlinkGame with SymlinkFolders: every subfolder of the game becomes a folder
// link (a junction when symbolic links need elevation). Used to check that cleanup never deletes through a link.
Hub.Handler.Version = 1;
Hub.Handler.Id = "coopdummy-symfolders-test";
Game.GameName = "Coop Dummy SymFolders";
Game.GUID = "CoopDummySymFolders";
Game.Description = "PlayOut Co-Op test game (symlinked folders).";
Game.SymlinkGame = true;
Game.SymlinkFolders = true;
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