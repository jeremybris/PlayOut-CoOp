using Nucleus.Gaming.Coop;
using System.Collections.Generic;
using System.IO;
using System.Reflection;


namespace Nucleus.Gaming.Tools.DevReorder
{
    public static class DevReorder
    {
        public static void UseDevReorder(PlayerInfo player, int i, bool setupDll)
        {
            var handlerInstance = GenericGameHandler.Instance;

            handlerInstance.Log("Setting up Devreorder");

            string utilFolder = Path.Combine(Globals.NucleusInstallRoot, "utils\\devreorder");

            if (setupDll)
            {
                string ogFile = Path.Combine(handlerInstance.instanceExeFolder, "dinput8.dll");
                FileUtil.FileCheck(ogFile);
                handlerInstance.Log("Copying dinput8.dll");
                File.Copy(Path.Combine(utilFolder, handlerInstance.garch + "\\dinput8.dll"), Path.Combine(handlerInstance.instanceExeFolder, "dinput8.dll"), true);
            }

            if (setupDll)
            {
                handlerInstance.addedFiles.Add(Path.Combine(handlerInstance.instanceExeFolder, "devreorder.ini"));
            }

            List<string> iniConfig = new List<string>();
            iniConfig.Add("[order]");
            iniConfig.Add("{" + player.GamepadGuid + "}");
            iniConfig.Add(string.Empty);
            iniConfig.Add("[hidden]");

            for (int p = 0; p < handlerInstance.profile.DevicesList.Count; p++)
            {
                if (p != i)
                {
                    iniConfig.Add("{" + handlerInstance.profile.DevicesList[p].GamepadGuid + "}");
                }
            }
            handlerInstance.Log("Writing devreorder.ini with the only visible gamepad guid: " + player.GamepadGuid);
            File.WriteAllLines(Path.Combine(handlerInstance.instanceExeFolder, "devreorder.ini"), iniConfig.ToArray());
            handlerInstance.Log("devreorder setup complete");
        }
    }
}
