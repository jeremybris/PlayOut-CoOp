// Modified for PlayOut Co-Op, 2026-11-06 (GPL-3 s.5a): remembers the AppCompatFlags\Layers values it sets so that
// RestoreAll() can undo them. See README.md "Changes from Nucleus Co-op". Original: Nucleus Co-op Master/NucleusGaming/Tools/Hdpi/GameDpiAwareness.cs
using Microsoft.Win32;
using Nucleus.Gaming.App.Settings;
using System;

namespace Nucleus.Gaming
{
    public enum DpiAwarenessMode
    {
        HighDpiAware,
        DpiUnaware,
        DpiAware,
        GdiDpiScaling_DpiUnaware,
        GdiDpiScaling_DpiUnaware_GdiScale
    }

    public static class GameDpiAwareness
    {
        //~ HIGHDPIAWARE → The application handles DPI scaling itself.
        //~ DPIUNAWARE → Disables all DPI scaling.
        //~ DPIAWARE → Enables DPI compatibility.
        //~ GDIDPISCALING DPIUNAWARE → Windows applies software-based DPI scaling.
        //~ GDIDPISCALING DPIUNAWARE GDISCALE → Forces GDI-based scaling.

        public static void SetExeDpiAwareness(string exePath)
        {
            var genericGameInfo = GenericGameHandler.Instance?.CurrentGameInfo;

            if(genericGameInfo == null)
            {
                return;
            }

            string dpiAwareness = string.Empty; 

            switch (genericGameInfo.DpiAwarenessMode)
            {
                case DpiAwarenessMode.HighDpiAware:
                    dpiAwareness = "~ HIGHDPIAWARE";
                    break;
                case DpiAwarenessMode.DpiUnaware:
                    dpiAwareness = "~ DPIUNAWARE";
                    break;
                case DpiAwarenessMode.DpiAware:
                    dpiAwareness = "~ DPIAWARE";
                    break;
                case DpiAwarenessMode.GdiDpiScaling_DpiUnaware:
                    dpiAwareness = "~ GDIDPISCALING DPIUNAWARE";
                    break;
                case DpiAwarenessMode.GdiDpiScaling_DpiUnaware_GdiScale:
                    dpiAwareness = "~ GDIDPISCALING DPIUNAWARE GDISCALE";
                    break;
            }

            string registryPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    if (key != null)
                    {
                        // PlayOut Co-Op: remember what was there so a headless session can put it back when it ends.
                        lock (Changed)
                        {
                            if (!Changed.ContainsKey(exePath))
                            {
                                Changed[exePath] = key.GetValue(exePath) as string;
                            }
                        }

                        key.SetValue(exePath, dpiAwareness, RegistryValueKind.String);

                        if(App_Misc.DebugLog)
                        {
                            LogManager.Log($"DPI awareness set to {dpiAwareness} for {exePath}");
                        }             
                    }
                    else
                    {
                        if (App_Misc.DebugLog)
                        {
                            LogManager.Log("Can't access Windows Registry.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Log($"Error : {ex.Message}");
            }
        }

        // PlayOut Co-Op: AppCompatFlags\Layers values set by SetExeDpiAwareness -> their previous data (null = absent).
        private static readonly System.Collections.Generic.Dictionary<string, string> Changed =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// PlayOut Co-Op: undo every SetExeDpiAwareness of this process (values that did not exist are deleted, the
        /// others get their old data back). Called by the headless session when it ends.
        /// </summary>
        public static void RestoreAll()
        {
            lock (Changed)
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", true))
                    {
                        if (key == null)
                        {
                            return;
                        }

                        foreach (var kv in Changed)
                        {
                            try
                            {
                                if (kv.Value == null)
                                {
                                    key.DeleteValue(kv.Key, false);
                                }
                                else
                                {
                                    key.SetValue(kv.Key, kv.Value, RegistryValueKind.String);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogManager.Log($"Could not restore the DPI value of {kv.Key}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Log($"Could not restore DPI values: {ex.Message}");
                }

                Changed.Clear();
            }
        }
    }
}
