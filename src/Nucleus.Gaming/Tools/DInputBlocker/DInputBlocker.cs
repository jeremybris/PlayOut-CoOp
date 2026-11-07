using System.IO;
using System.Reflection;

namespace Nucleus.Gaming.Tools.DInputBlocker
{
    public static class DInputBlocker
    {
        public static void UseDInputBlocker(bool setupDll)
        {
            var handlerInstance = GenericGameHandler.Instance;
            handlerInstance.Log("Setting up DInput blocker");
            string utilFolder = Path.Combine(Globals.NucleusInstallRoot, "utils\\dinput8.blocker");

            if (setupDll)
            {
                string ogFile = Path.Combine(handlerInstance.instanceExeFolder, "dinput8.dll");
                FileUtil.FileCheck(ogFile);

                handlerInstance.Log("Copying dinput8.dll");
                File.Copy(Path.Combine(utilFolder, handlerInstance.garch + "\\dinput8.dll"), Path.Combine(handlerInstance.instanceExeFolder, "dinput8.dll"), true);
            }
        }

    }
}
