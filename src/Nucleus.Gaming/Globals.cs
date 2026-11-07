using Nucleus.Gaming.App.Settings;
using Nucleus.Gaming.Controls;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Nucleus.Gaming
{
    public static class Globals
    {
        public const string Version = "2.4.1";

        // PlayOut Co-Op: Settings.ini follows NucleusInstallRoot (Nucleus used the current directory, which is
        // the install root when Nucleus runs normally). Re-created when the root changes.
        private static IniFile _ini;
        public static IniFile ini => _ini ?? (_ini = new IniFile(Path.Combine(NucleusInstallRoot, "Settings.ini")));

        //returns theme path(current theme folder)
        public static string ThemeFolder => Path.Combine(NucleusInstallRoot, $@"gui\theme\{App_Misc.Theme}\");

        //returns theme.ini file(current theme)
        public static IniFile ThemeConfigFile => new IniFile(Path.Combine(ThemeFolder, "theme.ini"));

        public static string GameProfilesFolder => Path.Combine(NucleusInstallRoot, $"game profiles");
       
        public static readonly int NucleusMaxPlayers = 32;

        // PlayOut Co-Op: the install root (handlers\, utils\, content\, hook DLLs, Settings.ini) is configurable so the
        // engine can run from a read-only program folder against a writable "engine root". Defaults to the entry exe's
        // folder exactly like Nucleus.
        private static string nucleusInstallRoot;
        public static string NucleusInstallRoot
        {
            get => nucleusInstallRoot ?? Path.GetDirectoryName((Assembly.GetEntryAssembly() ?? typeof(Globals).Assembly).Location);
            set
            {
                nucleusInstallRoot = string.IsNullOrEmpty(value) ? null : Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar);
                _ini = null;
            }
        }

        //Used so any launcher can add its own "main window" handle without worring about catching the window manually.
        public static IntPtr MainWindowHandle;

        public static string UserEnvironmentRoot => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static string UserDocumentsRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public static bool IsOneDriveEnabled = UserDocumentsRoot.Contains("OneDrive");

        public static WPF_OSD MainOSD;
    }
}
