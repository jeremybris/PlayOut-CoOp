using Nucleus.Gaming.Coop.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nucleus.Gaming.App.Settings
{
    public static class App_Audio
    {
        private static Dictionary<string, string> instances_AudioOutput = new Dictionary<string, string>();
        public static Dictionary<string, string> Instances_AudioOutput => instances_AudioOutput;

        private static string custom;
        public static string Custom
        {
            get => custom;
            set
            {
                custom = value;
                Globals.ini.IniWriteValue("Audio", "Custom", value);
            }
        }

        public static void SaveAudioOutput(string key, string value)
        {
            Globals.ini.IniWriteValue("Audio", key, value);
            instances_AudioOutput[key] = value;
        }

        public static bool LoadSettings()
        {       
            for (int i = 0; i < 8; i++)
            {
                instances_AudioOutput.Add("AudioInstance" + (i + 1), Globals.ini.IniReadValue("Audio", "AudioInstance" + (i + 1)));
            }

            custom = Globals.ini.IniReadValue("Audio", "Custom");
            return true;
        }

    }
}
