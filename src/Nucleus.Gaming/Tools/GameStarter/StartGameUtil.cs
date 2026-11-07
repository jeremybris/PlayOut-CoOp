// Modified for PlayOut Co-Op, 2026-11-06 (GPL-3 s.5a): HardlinkGame without SymlinkFolders starts StartGame.exe
// without "runas" (no UAC prompt); StartGame() fails with a clear error instead of parsing a stale or missing PID line.
// Modified again 2026-11-07: SymlinkGame links in-process through Headless.GameLinks, never elevated, unless
// GameLinks.AllowElevation (job "allowElevation") asks for Nucleus's elevated StartGame.exe. UnlockGameFiles remembers the
// attributes it clears on the original game files; RestoreGameFileAttributes() puts them back when the session ends.
// Modified again 2026-11-07 (later): when files can only be copied (no symbolic or hard links), the copy is refused
// before it starts if it would not fit on the drive.
// See README.md "Changes from Nucleus Co-op". Original: Nucleus Co-op Master/NucleusGaming/Tools/GameStarter/StartGameUtil.cs
using Nucleus.Gaming.Forms;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Nucleus.Gaming.Tools.GameStarter
{
    /// <summary>
    /// Util class for executing and reading output from the Nucleus.Coop.StartGame application
    /// </summary>
    public static class StartGameUtil
    {
        private static string lastLine;
        private static object locker = new object();

        public static string GetStartGamePath()
        {
            return Path.Combine(Globals.NucleusInstallRoot, "StartGame.exe");
        }

        public static string GetArguments(string pathToGame, string args, int waitTime, string mutexType, params string[] mutex)
        {
            string mu = "";
            for (int i = 0; i < mutex.Length; i++)
            {
                mu += mutex[i];

                if (i != mutex.Length - 1)
                {
                    mu += "|";
                }
            }

            return "\"" + pathToGame + "\" \"" + args + "\" \"" + waitTime + "\" \"" + mutexType + "\" \"" + mu + "\"";
        }

        public static void KillMutex(Process p, string mutexType, bool partial, params string[] mutex)
        {
            lock (locker)
            {
                string startGamePath = GetStartGamePath();
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = startGamePath
                };

                string mu = "";
                for (int i = 0; i < mutex.Length; i++)
                {
                    mu += mutex[i];
                    if (i != mutex.Length - 1)
                    {
                        mu += "|==|";
                    }
                }

                startInfo.Arguments = "\"proc|::|" + p.Id.ToString() + "\" \"partialmutex|::|" + partial + "\" \"mutextype|::|" + mutexType + "\" \"mutex|::|" + mu + "\"";
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;

                Process proc = Process.Start(startInfo);
                proc.OutputDataReceived += Proc_OutputDataReceived;
                proc.BeginOutputReadLine();

                proc.WaitForExit();
            }
        }

        public static bool MutexExists(Process p, string mutexType, bool partial, params string[] mutex)
        {
            lock (locker)
            {
                string startGamePath = GetStartGamePath();
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = startGamePath
                };

                string mu = "";
                for (int i = 0; i < mutex.Length; i++)
                {
                    mu += mutex[i];

                    if (i != mutex.Length - 1)
                    {
                        mu += "|==|";
                    }
                }

                startInfo.Arguments = $"\"proc|::|{p.Id}\" \"partialmutex|::|{partial}\" \"mutextype|::|{mutexType}\" \"output|::|{mu}\"";
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;

                Process proc = Process.Start(startInfo);
                proc.OutputDataReceived += Proc_OutputDataReceived;
                proc.BeginOutputReadLine();

                proc.WaitForExit();

                bool.TryParse(lastLine, out bool result);

                return result;
            }
        }

        /// <summary>
        /// NOT THREAD SAFE
        /// </summary>
        /// <param name="pathToGame"></param>
        /// <param name="args"></param>
        /// <param name="waitTime"></param>
        /// <param name="mutex"></param>
        /// <returns></returns>
        public static uint StartGame(string pathToGame, string args, bool hook, bool delay, bool renameMutex, string mutexNames, bool setWindow, bool isDebug, string nucleusFolder, bool blockRaw, bool UseNucleusEnvironment, string playerNick, bool startupHooksEnabled, bool createSingle, string rawHid, int width, int height, int posx, int posy, string docpath, bool usedocs, /*bool runAdmin, string rawHid,*/ string workingDir = null)
        {
            lock (locker)
            {
                string startGamePath = GetStartGamePath();
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = startGamePath
                };

                if (!string.IsNullOrWhiteSpace(workingDir))
                {
                    workingDir = "|" + workingDir;
                }

                //string arguments = 
                startInfo.Arguments = "\"" + "hook|::|" + hook + "\" \"delay|::|" + delay + "\" \"renamemutex|::|" + renameMutex + "\" \"mutextorename|::|" + mutexNames + "\" \"setwindow|::|" + setWindow + "\" \"isdebug|::|" + isDebug + "\" \"nucleusfolderpath|::|" + nucleusFolder + "\" \"blockraw|::|" + blockRaw + "\" \"nucenv|::|" + UseNucleusEnvironment + "\" \"playernick|::|" + playerNick + "\" \"starthks|::|" + startupHooksEnabled + "\" \"createsingle|::|" + createSingle + "\" \"rawhid|::|" + rawHid + "\" \"width|::|" + width + "\" \"height|::|" + height + "\" \"posx|::|" + posx + "\" \"posy|::|" + posy + "\" \"docpath|::|" + docpath + "\" \"usedocs|::|" + usedocs  /*+ "\" \"rawhid|::|" + rawHid*/ + "\" \"game|::|" + pathToGame + workingDir + ";" + args + "\"";
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;

                if (!File.Exists(startGamePath))
                {
                    throw new FileNotFoundException("StartGame.exe is missing from the engine root.", startGamePath);
                }

                lastLine = null; // PlayOut Co-Op: never return the previous instance's PID
                Process proc = Process.Start(startInfo);

                proc.OutputDataReceived += Proc_OutputDataReceived;
                proc.BeginOutputReadLine();

                proc.WaitForExit();

                //parse the last line for the process ID
                string[] parts = lastLine?.Split(':');
                if (parts == null || parts.Length < 2 || !uint.TryParse(parts[parts.Length - 1].Trim(), out uint pid))
                {
                    throw new InvalidOperationException("StartGame.exe could not start the game" + (string.IsNullOrEmpty(lastLine) ? "." : ": " + lastLine));
                }

                return pid;
            }
        }

        public static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                return;

            }
            Console.WriteLine($"Redirected output: {e.Data}");
            lastLine = e.Data;
        }

        public static bool SymlinkGame(string root, string destination, out int exitCode,
            string[] dirExclusions, string[] fileExclusions, string[] fileCopyInstead, bool hardLink, bool symFolders, int numPlayers)
        {
            exitCode = 1;

            lock (locker)
            {
                string startGamePath = GetStartGamePath();

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = startGamePath
                };


                string de = "";
                for (int i = 0; i < dirExclusions.Length; i++)
                {
                    de += dirExclusions[i];
                    if (i != dirExclusions.Length - 1)
                    {
                        de += "|==|";
                    }
                }

                string fe = "";
                for (int i = 0; i < fileExclusions.Length; i++)
                {
                    fe += fileExclusions[i];
                    if (i != fileExclusions.Length - 1)
                    {
                        fe += "|==|";
                    }
                }

                string fc = "";
                for (int i = 0; i < fileCopyInstead.Length; i++)
                {
                    fc += fileCopyInstead[i];
                    if (i != fileCopyInstead.Length - 1)
                    {
                        fc += "|==|";
                    }
                }

                if (!Headless.GameLinks.AllowElevation)
                {
                    // PlayOut Co-Op: link in this process, never elevated (no UAC). Same walk as StartGame.exe's
                    // "symlink" command (WinDirectoryUtil.LinkDirectory per instance), with GameLinks choosing
                    // symbolic link / junction / hard link / copy per item.
                    LinkInProcess(root, destination, dirExclusions, fileExclusions, fileCopyInstead, hardLink, symFolders, numPlayers);
                    exitCode = 0;
                    return true;
                }

                startInfo.Arguments = "\"" + "root|::|" + root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) /*+ "\" \"currentdir|::|" + currentDir */+ "\" \"destination|::|" + destination + "\" \"direxclusions|::|" + de + "\" \"fileexclusions|::|" + fe + "\" \"filecopyinstead|::|" + fc + "\" \"hardlink|::|" + hardLink + "\" \"symfolders|::|" + symFolders + "\" \"numplayers|::|" + numPlayers /*+ "\" \"overridespecial|::|" + overrideSpecial*/ + "\" \"symlink|::|" + "true";
                //"\"" + "hook|::|" + hook + "\" \"delay|::|"
                startInfo.UseShellExecute = true;
                // PlayOut Co-Op: hard links to files need no elevation, so UAC is only requested when symbolic links
                // are made (SymlinkGame, or HardlinkGame with SymlinkFolders).
                if (!hardLink || symFolders)
                {
                    startInfo.Verb = "runas";
                }
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                Process proc = Process.Start(startInfo);//TODO: can throw error

                proc.WaitForExit();
            }

            return true;
        }

        // PlayOut Co-Op: what StartGame.exe does for "symlink", without a separate (elevated) process.
        private static void LinkInProcess(string root, string destination, string[] dirExclusions, string[] fileExclusions,
            string[] fileCopyInstead, bool hardLink, bool symFolders, int numPlayers)
        {
            // StartGame.exe receives the lists joined with "|==|" and splits them again: an empty list arrives as {""},
            // which WinDirectoryUtil relies on (it reads element 0).
            string[] Norm(string[] a) => a == null || a.Length == 0 ? new[] { "" } : a;

            string rootDir = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string parent = destination.Substring(0, destination.LastIndexOf('\\'));

            Headless.GameLinks.ResetCounters();
            Headless.EngineUI.Log("LINK", $"{(hardLink ? "Hard-linking" : "Linking")} {rootDir} into {numPlayers} instance folder(s) under {parent} (symlink folders: {symFolders}, no elevation)");
            CheckCopyFallback(rootDir, parent, hardLink, symFolders, numPlayers);
            Stopwatch sw = Stopwatch.StartNew();

            for (int p = 0; p < numPlayers; p++)
            {
                Platform.Windows.IO.WinDirectoryUtil.LinkDirectory(rootDir, new DirectoryInfo(rootDir), parent + "\\Instance" + p, out _,
                    Norm(dirExclusions), Norm(fileExclusions), Norm(fileCopyInstead), hardLink, symFolders);
            }

            Headless.EngineUI.Log("LINK", $"Instance folders ready in {sw.ElapsedMilliseconds} ms: {Headless.GameLinks.Summary()}");
        }

        // PlayOut Co-Op: when neither a symbolic link nor a hard link can be made from the game folder into the instance
        // folders (Developer Mode off and another drive, or a FAT/exFAT drive), every linked file would be copied for
        // every player. Refuse before copying anything if that does not fit on the drive (keeping
        // GameLinks.CopyReserveBytes free), and log how much is copied when it does.
        private static void CheckCopyFallback(string rootDir, string parent, bool hardLink, bool symFolders, int numPlayers)
        {
            string sample = null;
            try
            {
                foreach (string f in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
                {
                    sample = f;
                    break;
                }
            }
            catch (Exception ex)
            {
                Headless.EngineUI.Log("LINK", "Could not list the game folder for the link check: " + ex.Message);
                return;
            }

            if (sample == null || Headless.GameLinks.CanLinkFiles(sample, parent, hardLink))
            {
                return;
            }

            // With SymlinkFolders the subfolders become junctions, so only the files at the top are copied.
            long perInstance = 0;
            try
            {
                foreach (string f in Directory.EnumerateFiles(rootDir, "*", symFolders ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories))
                {
                    try { perInstance += new FileInfo(f).Length; } catch { }
                }
            }
            catch (Exception ex)
            {
                Headless.EngineUI.Log("LINK", "Could not measure the game folder: " + ex.Message);
            }

            long needed = perInstance * numPlayers;
            long free = Headless.GameLinks.FreeBytes(parent);
            string Gb(long b) => b >= 1073741824
                ? (b / 1073741824.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " GB"
                : (b / 1048576.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
            Headless.EngineUI.Log("LINK", $"Neither symbolic nor hard links work from {rootDir} into {parent}: the game files are copied " +
                $"({Gb(needed)} for {numPlayers} instance(s), {(free < 0 ? "unknown" : Gb(free))} free)");

            if (free >= 0 && needed > free - Headless.GameLinks.CopyReserveBytes)
            {
                throw new IOException($"The game cannot be linked into {parent} (symbolic links need Windows Developer Mode, hard links need the " +
                    $"same NTFS drive as the game) and copying it would need {Gb(needed)} with only {Gb(free)} free ({Gb(Headless.GameLinks.CopyReserveBytes)} is always kept free). Turn on Developer Mode, or put " +
                    "the instance folders (job \"contentRoot\") on the game's NTFS drive.");
            }
        }

        public static bool UnlockGameFiles(string origGameDir)
        {
            if (!Directory.Exists(origGameDir))
            {
                NucleusMessageBox.Show("",
                                           $"Directory not found:\n"+
                                           $"{origGameDir}", false);
                return false;
            }

            string[] fileSystemEntries = Directory.GetFileSystemEntries(origGameDir, "*", SearchOption.AllDirectories);

            foreach (string entry in fileSystemEntries)
            {
                if(entry.Length > 256)
                {
                    NucleusMessageBox.Show("File path is too long!", 
                                           $"The file path is too long.\n" +
                                           $"{entry}\n" + 
                                           $"Limit is 256 characters and the file path is {entry.Length} characters long.\n" +
                                           $"You can try moving your game at the root of your drive to reduce its length.",false);
                    return false;
                }

                if(!File.Exists(entry))
                {
                    continue;
                }

                // PlayOut Co-Op: remember what we change in the original game folder, to put it back at the end.
                FileAttributes before = File.GetAttributes(entry);
                if (before != FileAttributes.Normal)
                {
                    lock (unlockedLock)
                    {
                        if (!unlockedAttributes.ContainsKey(entry))
                        {
                            unlockedAttributes[entry] = before;
                        }
                    }
                }

                File.SetAttributes(entry, FileAttributes.Normal);
            }

            return true;
        }

        // PlayOut Co-Op: original attributes of the game files UnlockGameFiles reset (read-only, archive, hidden...).
        private static readonly object unlockedLock = new object();
        private static readonly System.Collections.Generic.Dictionary<string, FileAttributes> unlockedAttributes =
            new System.Collections.Generic.Dictionary<string, FileAttributes>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// PlayOut Co-Op: puts back the attributes UnlockGameFiles cleared on the original game files (Nucleus leaves
        /// them all "Normal"). Called when the session has ended. Returns how many files were restored.
        /// </summary>
        public static int RestoreGameFileAttributes()
        {
            int restored = 0;
            lock (unlockedLock)
            {
                foreach (System.Collections.Generic.KeyValuePair<string, FileAttributes> kv in unlockedAttributes)
                {
                    try
                    {
                        if (File.Exists(kv.Key) && File.GetAttributes(kv.Key) != kv.Value)
                        {
                            File.SetAttributes(kv.Key, kv.Value);
                            restored++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Headless.EngineUI.Log("LINK", $"Could not restore the attributes of {kv.Key}: {ex.Message}");
                    }
                }

                unlockedAttributes.Clear();
            }

            return restored;
        }
    }
}
