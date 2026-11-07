// Modified for PlayOut Co-Op, 2026-11-07 (GPL-3 s.5a): the game content folder (instance folders) follows GameManager.ContentRootOverride (job "contentRoot").
// See README.md "Changes from Nucleus Co-op". Original: Nucleus Co-op Master/NucleusGaming/Tools/DInput8CoopLvlUnlocker/DInput8CoopLvlUnlocker.cs
using Nucleus.Gaming.Coop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.Gaming.Tools.DInput8CoopLvlUnlocker
{
    internal class DInput8CoopLvlUnlocker
    {
        public static void SetupDInput8CoopLvlUnlocker(PlayerInfo player, int i, bool setupDll)
        {
            var handlerInstance = GenericGameHandler.Instance;
            string gameGUID = handlerInstance.CurrentGameInfo.GUID;

            string gameContentPath = GameManager.Instance.GetGameContentPath(gameGUID) /* PlayOut Co-Op */;//game content root

            string dllsPath = Path.Combine(Globals.NucleusInstallRoot, "utils\\DInput8CoopLvlUnlocker");
            string dllName = "dinput8.dll";

             handlerInstance.Log($"Setting up \"Dinput8 CoopLevel Unlocker\" for player {i}...");

            if (setupDll)
            {
                try
                {
                    File.Copy(Path.Combine(dllsPath, handlerInstance.garch + "\\" + dllName), Path.Combine(handlerInstance.instanceExeFolder, dllName), true);
                }
                catch (Exception ex)
                {
                    handlerInstance.Log("ERROR - " + ex.Message);
                    handlerInstance.Log("Using alternative copy method for " + dllName);
                    CmdUtil.ExecuteCommand(dllsPath, out int exitCode, "copy \"" + Path.Combine(dllsPath, handlerInstance.garch + "\\" + dllName) + "\" \"" + Path.Combine(handlerInstance.instanceExeFolder, dllName) + "\"");
                }
            }

        }
    }
}
