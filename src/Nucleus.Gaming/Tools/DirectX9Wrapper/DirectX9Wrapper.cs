using System.IO;
using System.Reflection;

namespace Nucleus.Gaming.Tools.DirectX9Wrapper
{
    public static class DirectX9Wrapper
    {
        public static void UseDirectX9Wrapper(bool setupDll)
        {
            var handlerInstance = GenericGameHandler.Instance;
            if (setupDll)
            {
                handlerInstance.Log("Copying over DirectX 9, Direct 3D Wrapper (d3d9.dll) to instance executable folder");
                string utilFolder = Path.Combine(Globals.NucleusInstallRoot, "utils\\DirectXWrapper");
                string ogFile = Path.Combine(handlerInstance.instanceExeFolder, "d3d9.dll");

                FileUtil.FileCheck(ogFile);
                File.Copy(Path.Combine(utilFolder, "d3d9.dll"), ogFile, true);
            }
        }
    }
}
