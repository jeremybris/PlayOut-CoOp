// Test handler for CoopDummy.exe that asks the player things: the launcher-exe file picker (LauncherExe =
// "NucleusDefined"), a handler text prompt (CustomUserGeneralPrompts), a "Press OK" pause between instances
// (PromptBetweenInstances) and a Yes/No question (through the engine's EngineUI from the script).
Hub.Handler.Version = 1;
Hub.Handler.Id = "coopdummy-prompts-test";
Game.GameName = "Coop Dummy Prompts";
Game.GUID = "CoopDummyPrompts";
Game.Description = "PlayOut Co-Op test game (prompts).";
Game.SymlinkGame = true;
Game.LauncherExe = "NucleusDefined";
Game.CustomUserGeneralPrompts = ["Test name for the dummy"];
Game.PromptBetweenInstances = true;
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
    var answer = "none";
    if (Context.PlayerID == 0) {
        try {
            var ns = importNamespace("Nucleus.Gaming.Headless");
            answer = "" + ns.EngineUI.MessageBox("Test question: continue?", "Coop Dummy question", 4, 32, 0, 0, false, "test.js", "Play", 0);
        } catch (e) { answer = "error-" + e; }
    }
    Context.StartArguments = "--player " + Context.PlayerID + " --mode " + Context.Options["mode"] + " --name " + Context.CustomUserGeneralValues[0] + " --answer " + answer;
};