// Modified for PlayOut Co-Op, 2026-11-06 (GPL-3 s.5a): headless changes (dialogs/OSD through Headless.EngineUI,
// configurable install root). See README.md "Changes from Nucleus Co-op". Original: Nucleus Co-op Master/NucleusGaming/Tools/CleanGameContent/CleanGameContent.cs
// Modified again 2026-11-07: content deletion never follows links/junctions (Headless.GameLinks.DeleteTree); contentRoot path.
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.IO;
using System.Linq;
using Nucleus.Gaming.Coop.Generic;
using Nucleus.Gaming.Forms;
using System.Management.Instrumentation;
using System.Windows.Forms;

using Nucleus.Gaming.Headless;

namespace Nucleus.Gaming
{
    public static class CleanGameContent
    {

        public static void CleanContentFolder(GenericGameInfo currentGameInfo, bool askBefore)
        {
            string path = currentGameInfo.Content_Folder;
            bool contentExist = Directory.Exists(path);

            if (askBefore)
            {
                if (contentExist)
                {
                    DialogResult dialogResult = EngineUI.MessageBox("Are you sure you want to delete '" + path + "' and all its contents?", "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dialogResult == DialogResult.Yes)
                    {
                        CleanContentFolder(currentGameInfo,path);
                    }
                }
                else
                {
                    EngineUI.MessageBox("No data in content folder to delete.");
                }
            }
            else
            {
                if(contentExist)
                CleanContentFolder(currentGameInfo,path);
            }
        }


        private static void CleanContentFolder(GenericGameInfo currentGameInfo, string path)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                KillRemainingGameProcess(currentGameInfo);
                DeleteSafely(path); // PlayOut Co-Op: never follows links/junctions into the game folder
            });
        }

        public static void CleanContentFolder(GenericGameInfo currentGameInfo)
        {
            // PlayOut Co-Op: content\<GUID> (or <contentRoot>\<GUID>), deleted without ever following a symbolic link
            // or junction into the original game folder (Nucleus's fallback enumerated through links, cleared
            // attributes and recursively deleted folders under them).
            string path = GameManager.Instance.GetGameContentPath(currentGameInfo.GUID);

            if (Directory.Exists(path))
            {
                DeleteSafely(path);
            }
        }

        /// <summary>PlayOut Co-Op: GameLinks.DeleteTree with a few retries for files the exiting game still holds.</summary>
        private static void DeleteSafely(string path)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    Headless.GameLinks.DeleteTree(path);
                    LogManager.Log("Game content cleaned.");
                    Headless.EngineUI.Log("LINK", "Deleted " + path);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt >= 5)
                    {
                        LogManager.Log("Game content cleanup failed: " + ex.Message);
                        Headless.EngineUI.Log("LINK", "Could not delete " + path + ": " + ex.Message);
                        return;
                    }

                    System.Threading.Thread.Sleep(1000);
                }
            }
        }
        private static void KillRemainingGameProcess(GenericGameInfo currentGameInfo)
        {
            try
            {
                Process[] procs = Process.GetProcesses();

                List<string> addtlProcsToKill = new List<string>();
                if (currentGameInfo.KillProcessesOnClose?.Length > 0)
                {
                    addtlProcsToKill = currentGameInfo.KillProcessesOnClose.ToList();
                }

                foreach (Process proc in procs)
                {
                    try
                    {
                        if ((currentGameInfo.LauncherExe != null && !currentGameInfo.LauncherExe.Contains("NucleusDefined") && proc.ProcessName.ToLower() == Path.GetFileNameWithoutExtension(currentGameInfo.LauncherExe.ToLower())) ||
                            addtlProcsToKill.Contains(proc.ProcessName, StringComparer.OrdinalIgnoreCase) || 
                            proc.ProcessName.ToLower() == Path.GetFileNameWithoutExtension(currentGameInfo.ExecutableName.ToLower()) || (currentGameInfo.Hook.ForceFocusWindowName != "" && proc.MainWindowTitle == currentGameInfo.Hook.ForceFocusWindowName))
                        {
                            LogManager.Log(string.Format("Killing process {0} (pid {1})", proc.ProcessName, proc.Id));
                            proc.Kill();
                        }

                    }
                    catch (Exception ex)
                    {
                        LogManager.Log(ex.InnerException + " " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(ex.InnerException + " " + ex.Message);
            }
        }
    }
}
