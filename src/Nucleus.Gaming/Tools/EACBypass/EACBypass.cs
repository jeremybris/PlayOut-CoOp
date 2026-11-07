using System.IO;
using System.Reflection;

namespace Nucleus.Gaming.Tools.EACBypass
{
    public static class EACBypass
    {

        public static void UseEACBypass(string linkFolder, bool setupDll)
        {
            var handlerInstance = GenericGameHandler.Instance;

            if (setupDll)
            {
                handlerInstance.Log("Starting EAC Bypass setup");

                string utilFolder = Path.Combine(Globals.NucleusInstallRoot, "utils\\EAC Bypass");

                string[] eac64DllFiles = Directory.GetFiles(linkFolder, "EasyAntiCheat_x64.dll", SearchOption.AllDirectories);
                foreach (string nameFile in eac64DllFiles)
                {
                    handlerInstance.Log("Found " + nameFile);
                    string dir = Path.GetDirectoryName(nameFile);

                    FileUtil.FileCheck(nameFile);
                    File.Copy(Path.Combine(utilFolder, "EasyAntiCheat_x64.dll"), Path.Combine(dir, "EasyAntiCheat_x64.dll"), true);
                }

                string[] eac86DllFiles = Directory.GetFiles(linkFolder, "EasyAntiCheat_x86.dll", SearchOption.AllDirectories);
                foreach (string nameFile in eac86DllFiles)
                {
                    handlerInstance.Log("Found " + nameFile);
                    string dir = Path.GetDirectoryName(nameFile);

                    FileUtil.FileCheck(nameFile);

                    File.Copy(Path.Combine(utilFolder, "EasyAntiCheat_x86.dll"), Path.Combine(dir, "EasyAntiCheat_x86.dll"), true);
                }
            }
        }
    }
}
