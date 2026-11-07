using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.Gaming.App.Settings
{
    public static class App_Layouts
    {
        private static bool windowsMerger;
        public static bool WindowsMerger
        {
            get => windowsMerger;
            set
            {
                windowsMerger = value; Globals.ini.IniWriteValue("CustomLayout", "WindowsMerger", value.ToString());
            }
        }

        private static string windowsMergerRes;
        public static string WindowsMergerRes
        {
            get => windowsMergerRes;
            set
            {
                windowsMergerRes = value;
                Globals.ini.IniWriteValue("CustomLayout", "WindowsMergerRes", value);
            }
        }

        private static bool losslessHook;
        public static bool LosslessHook
        {
            get => losslessHook;
            set
            {
                losslessHook = value;
                Globals.ini.IniWriteValue("CustomLayout", "LosslessHook", value.ToString());
            }
        }

        private static bool splitDiv;
        public static bool SplitDiv
        {
            get => splitDiv;
            set
            {
                splitDiv = value;
                Globals.ini.IniWriteValue("CustomLayout", "SplitDiv", value.ToString());
            }
        }

        private static bool hideOnly;
        public static bool HideOnly
        {
            get => hideOnly;
            set
            {
                hideOnly = value;
                Globals.ini.IniWriteValue("CustomLayout", "HideOnly", value.ToString());
            }
        }

        private static string splitDivColor;
        public static string SplitDivColor
        {
            get => splitDivColor;
            set
            {
                splitDivColor = value;
                Globals.ini.IniWriteValue("CustomLayout", "SplitDivColor", value);
            }
        }

        private static int horizontalLines;
        public static int HorizontalLines
        {
            get => horizontalLines;
            set
            {
                horizontalLines = value;
                Globals.ini.IniWriteValue("CustomLayout", "HorizontalLines", value.ToString());
            }
        }

        private static int verticalLines;
        public static int VerticalLines
        {
            get => verticalLines;
            set
            {
                verticalLines = value; Globals.ini.IniWriteValue("CustomLayout", "VerticalLines", value.ToString());
            }
        }

        private static int maxPlayers;
        public static int MaxPlayers
        {
            get => maxPlayers;
            set
            {
                maxPlayers = value;
                Globals.ini.IniWriteValue("CustomLayout", "MaxPlayers", value.ToString());
            }
        }

        private static bool cts_KeepAspectRatio;
        public static bool Cts_KeepAspectRatio
        {
            get => cts_KeepAspectRatio;
            set
            {
                cts_KeepAspectRatio = value;
                Globals.ini.IniWriteValue("CustomLayout", "Cts_KeepAspectRatio", value.ToString());
            }
        }

        private static bool cts_MuteAudioOnly;
        public static bool Cts_MuteAudioOnly
        {
            get => cts_MuteAudioOnly;
            set
            {
                cts_MuteAudioOnly = value;
                Globals.ini.IniWriteValue("CustomLayout", "Cts_MuteAudioOnly", value.ToString());
            }
        }

        private static bool cts_Unfocus;
        public static bool Cts_Unfocus
        {
            get => cts_Unfocus;
            set
            {
                cts_Unfocus = value;
                Globals.ini.IniWriteValue("CustomLayout", "Cts_Unfocus", value.ToString());
            }
        }

        public static bool LoadSettings()
        {
            windowsMerger = bool.Parse(Globals.ini.IniReadValue("CustomLayout", "WindowsMerger"));
            windowsMergerRes = Globals.ini.IniReadValue("CustomLayout", "WindowsMergerRes");
            losslessHook = bool.Parse(Globals.ini.IniReadValue("CustomLayout", "LosslessHook"));
            splitDiv = bool.Parse(Globals.ini.IniReadValue("CustomLayout", "SplitDiv"));
            hideOnly = bool.Parse(Globals.ini.IniReadValue("CustomLayout", "HideOnly"));
            splitDivColor = Globals.ini.IniReadValue("CustomLayout", "SplitDivColor");
            horizontalLines = int.Parse(Globals.ini.IniReadValue("CustomLayout", "HorizontalLines"));
            verticalLines = int.Parse(Globals.ini.IniReadValue("CustomLayout", "VerticalLines"));
            maxPlayers = int.Parse(Globals.ini.IniReadValue("CustomLayout", "MaxPlayers"));
            cts_KeepAspectRatio = bool.Parse(Globals.ini.IniReadValue("CustomLayout", "Cts_KeepAspectRatio"));
            cts_MuteAudioOnly = bool.Parse(Globals.ini.IniReadValue("CustomLayout", "Cts_MuteAudioOnly"));
            cts_Unfocus = bool.Parse(Globals.ini.IniReadValue("CustomLayout", "Cts_Unfocus"));

            return true;
        }

    }
}
