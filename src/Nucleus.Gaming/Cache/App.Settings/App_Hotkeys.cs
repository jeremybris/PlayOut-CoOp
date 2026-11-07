using Nucleus.Gaming.Coop.InputManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.Gaming.App.Settings
{
    public static class App_Hotkeys
    {
        private static string[] close;
        public static string[] CloseApp
        {
            get => close;
            set
            {
                close = value;
                Globals.ini.IniWriteValue("Hotkeys", "Close", $"{value[0]}+{value[1]}");
            }
        }
    
        private static string[] stop;
        public static string[] StopSession 
        {
            get => stop; 
            set 
            { 
                stop = value; 
                Globals.ini.IniWriteValue("Hotkeys", "Stop", $"{value[0]}+{value[1]}");
            } 
        }

        private static string[] topMost;
        public static string[] TopMost 
        { 
            get => topMost; 
            set
            {
                topMost = value; 
                Globals.ini.IniWriteValue("Hotkeys", "TopMost", $"{value[0]}+{value[1]}"); 
            }
        }

        private static string[] setFocus;
        public static string[] SetFocus 
        { 
            get => setFocus; 
            set 
            { 
                setFocus = value;
                Globals.ini.IniWriteValue("Hotkeys", "SetFocus", $"{value[0]}+{value[1]}"); 
            } 
        }

        private static string[] resetWindows;
        public static string[] ResetWindows 
        { 
            get => resetWindows; 
            set 
            { 
                resetWindows = value; 
                Globals.ini.IniWriteValue("Hotkeys", "ResetWindows", $"{value[0]}+{value[1]}"); 
            } 
        }

        private static string[] cutscenes;
        public static string[] CutscenesMode 
        { 
            get => cutscenes; 
            set 
            { 
                cutscenes = value; 
                Globals.ini.IniWriteValue("Hotkeys", "Cutscenes", $"{value[0]}+{value[1]}");
            } 
        }

        private static string[] _switch;
        public static string[] SwitchLayout 
        { 
            get => _switch; 
            set 
            { 
                _switch = value; 
                Globals.ini.IniWriteValue("Hotkeys", "Switch", $"{value[0]}+{value[1]}"); 
            } 
        }

        private static string[] shortcutsReminder;
        public static string[] ShortcutsReminder 
        { 
            get => shortcutsReminder; 
            set
            { 
                shortcutsReminder = value; 
                Globals.ini.IniWriteValue("Hotkeys", "ShortcutsReminder", $"{value[0]}+{value[1]}"); 
            } 
        }

        private static string[] switchMergerChildForeGround;
        public static string[] SwitchMergerForeGroundChild
        { 
            get => switchMergerChildForeGround; 
            set 
            {
                switchMergerChildForeGround = value; 
                Globals.ini.IniWriteValue("Hotkeys", "SwitchMergerChildForeGround", $"{value[0]}+{value[1]}");
            } 
        }

        private static string lockInputs;
        public static string LockInputs
        { 
            get => lockInputs; 
            set 
            { 
                lockInputs = value; 
                Globals.ini.IniWriteValue("Hotkeys", "LockKey", value);
                ParseLockKey();
            } 
        }

        private static IDictionary<string, int> lockKeysDict = new Dictionary<string, int>
        {
                    { "End", 0x23 },
                    { "Home", 0x24 },
                    { "Delete", 0x2E },
                    { "Multiply", 0x6A },
                    { "F1", 0x70 },
                    { "F2", 0x71 },
                    { "F3", 0x72 },
                    { "F4", 0x73 },
                    { "F5", 0x74 },
                    { "F6", 0x75 },
                    { "F7", 0x76 },
                    { "F8", 0x77 },
                    { "F9", 0x78 },
                    { "F10", 0x79 },
                    { "F11", 0x7A },
                    { "F12", 0x7B },
                    { "+", 0xBB },
                    { "-", 0xBD },
                    { "Numpad 0", 0x60 },
                    { "Numpad 1", 0x61 },
                    { "Numpad 2", 0x62 },
                    { "Numpad 3", 0x63 },
                    { "Numpad 4", 0x64 },
                    { "Numpad 5", 0x65 },
                    { "Numpad 6", 0x66 },
                    { "Numpad 7", 0x67 },
                    { "Numpad 8", 0x68 },
                    { "Numpad 9", 0x69 }
        };

        public static int LockKeyValue { get; private set; }

        private static void ParseLockKey()
        {
            int key = lockKeysDict.Where(k => k.Key == LockInputs).FirstOrDefault().Value;
            LockKeyValue = key == 0 ? 0x23 : key;
        }

        public static bool LoadSettings()
        {
                close = new string[] { Globals.ini.IniReadValue("Hotkeys", "Close").Split('+')[0], Globals.ini.IniReadValue("Hotkeys", "Close").Split('+')[1]};
                stop = new string[] { Globals.ini.IniReadValue("Hotkeys", "Stop").Split('+')[0], Globals.ini.IniReadValue("Hotkeys", "Stop").Split('+')[1] };
                topMost = new string[] { Globals.ini.IniReadValue("Hotkeys", "TopMost").Split('+')[0], Globals.ini.IniReadValue("Hotkeys", "TopMost").Split('+')[1] };
                setFocus = new string[] { Globals.ini.IniReadValue("Hotkeys", "SetFocus").Split('+')[0], Globals.ini.IniReadValue("Hotkeys", "SetFocus").Split('+')[1] };
                resetWindows = new string[] { Globals.ini.IniReadValue("Hotkeys", "ResetWindows").Split('+')[0], Globals.ini.IniReadValue("Hotkeys", "ResetWindows").Split('+')[1] };
                cutscenes = new string[] { Globals.ini.IniReadValue("Hotkeys", "Cutscenes").Split('+')[0], Globals.ini.IniReadValue("Hotkeys", "Cutscenes").Split('+')[1] };
                _switch = new string[] { Globals.ini.IniReadValue("Hotkeys", "Switch").Split('+')[0], Globals.ini.IniReadValue("Hotkeys", "Switch").Split('+')[1] };
                shortcutsReminder = new string[] { Globals.ini.IniReadValue("Hotkeys", "ShortcutsReminder").Split('+')[0], Globals.ini.IniReadValue("Hotkeys", "ShortcutsReminder").Split('+')[1] };
                switchMergerChildForeGround = new string[] { Globals.ini.IniReadValue("Hotkeys", "SwitchMergerChildForeGround").Split('+')[0], Globals.ini.IniReadValue("Hotkeys", "SwitchMergerChildForeGround").Split('+')[1] };
                lockInputs = Globals.ini.IniReadValue("Hotkeys", "LockKey");
                ParseLockKey();

                return true;
        }
    }
}
