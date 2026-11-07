// Modified for PlayOut Co-Op, 2026-11-07 (GPL-3 s.5a): the game content folder (instance folders) follows GameManager.ContentRootOverride (job "contentRoot").
// Modified again 2026-11-07: backup and restoration rewritten with plain relative paths (upstream located the player's
// part of a path by searching for the nickname, which a file name containing it broke, and matched instance folders to
// players by sorted folder name, which put Instance10 before Instance2); one failed file no longer stops the rest; a
// restoration never writes through a link into the original game folder; messages go to the engine log.
// Modified again 2026-11-07: a file the game deleted during the session is removed from the backup too (deleted
// profiles no longer come back); a restoration never replaces an instance file newer than the backup (a session that
// ended without its backup keeps its progress); replacing a hard-linked file no longer clears the original's
// attributes; an unusable nickname or an unexpected error skips that player instead of aborting the launch or End();
// Context.BackupFiles/BackupFolders (a copy of Game's) are no longer backed up a second time.
// See README.md "Changes from Nucleus Co-op". Original: Nucleus Co-op Master/NucleusGaming/Tools/BackupDatas/BackupDatas.cs
using Microsoft.Win32.SafeHandles;
using Nucleus.Gaming.Coop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Nucleus.Gaming.Tools.BackupDatas
{
    /// <summary>
    /// Game.BackupFiles / Game.BackupFolders (and Context.*): files the game writes into its instance folders (saves,
    /// settings) are copied to %USERPROFILE%\NucleusCoop\_Game Files Backup_\&lt;GUID&gt;\&lt;nickname&gt; when the session ends,
    /// and copied back into the same player's instance folder when the next session starts.
    /// </summary>
    public static class BackupDatas
    {
        private static readonly string BackupDirectory = $@"{Globals.UserEnvironmentRoot}\NucleusCoop\_Game Files Backup_";

        // Instance files that had a backed up copy and were in the instance folder once the restoration ended. One
        // of them missing at the backup was deleted by the game during the session, so its backed up copy goes too.
        private static readonly HashSet<string> presentAfterRestore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object presentLock = new object();

        private static string PlayerBackupFolder(string gameGUID, string nickname) => Path.Combine(BackupDirectory, gameGUID, nickname);

        private static string InstanceFolder(string gameContentPath, int index) => Path.Combine(gameContentPath, $"Instance{index}");

        /// <summary>A path relative to a handler's game folder ("savedata\SaveGames1"), cleaned of leading separators.</summary>
        private static string Relative(string path) => path.Replace('/', '\\').Trim().TrimStart('\\');

        /// <summary>A nickname usable as one folder name (never null, empty, "." / ".." or with path characters).</summary>
        private static bool UsableNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname) || nickname == "." || nickname == "..")
            {
                return false;
            }

            return nickname.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private static bool IsLink(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        /// <summary>Whether the file has more than one name (a hard link to the original game file). True when unknown.</summary>
        private static bool IsHardLinked(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    return !GetFileInformationByHandle(fs.SafeFileHandle, out BY_HANDLE_FILE_INFORMATION info) || info.NumberOfLinks > 1;
                }
            }
            catch
            {
                return true;
            }
        }

        /// <summary>Whether a folder between <paramref name="root"/> (excluded) and <paramref name="directory"/> is a link.</summary>
        private static bool BehindLink(string root, string directory)
        {
            string current = Path.GetFullPath(directory).TrimEnd('\\');
            string top = Path.GetFullPath(root).TrimEnd('\\');
            while (current.Length > top.Length && current.StartsWith(top, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(current) && IsLink(current))
                {
                    return true;
                }

                current = Path.GetDirectoryName(current) ?? top;
            }

            return false;
        }

        /// <summary>
        /// Copy one file, replacing what is there. A link or hard link at the destination is removed (only that name),
        /// never written through, and the attributes it shares with the original game file are left alone.
        /// </summary>
        private static bool CopyFile(string source, string destination)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (File.Exists(destination))
                {
                    Headless.GameLinks.DeleteTree(destination); // a file: DeleteFile, read-only cleared on this name only
                }

                File.Copy(source, destination, true);
                return true;
            }
            catch (Exception ex)
            {
                Log($"Could not copy {source} to {destination}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove from the backup the files under <paramref name="relative"/> the game deleted during this session:
        /// in the instance folder after the restoration, missing now. A file that was not restored is always kept.
        /// </summary>
        private static int PruneDeleted(string destRoot, string instance, string relative)
        {
            string backup = Path.Combine(destRoot, relative);
            string[] files;
            if (File.Exists(backup))
            {
                files = new[] { backup };
            }
            else if (Directory.Exists(backup))
            {
                try
                {
                    files = Directory.GetFiles(backup, "*", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    Log($"Could not list {backup}: {ex.Message}");
                    return 0;
                }
            }
            else
            {
                return 0;
            }

            int removed = 0;
            foreach (string file in files)
            {
                string instanceFile = Path.Combine(instance, file.Substring(destRoot.Length).TrimStart('\\'));
                bool wasRestored;
                lock (presentLock)
                {
                    wasRestored = presentAfterRestore.Contains(instanceFile);
                }

                if (!wasRestored || File.Exists(instanceFile))
                {
                    continue;
                }

                try
                {
                    File.SetAttributes(file, FileAttributes.Normal); // a plain copy in the backup folder, never a link
                    File.Delete(file);
                    removed++;
                    Log($"Removed {file} from the backup: the game deleted it");
                }
                catch (Exception ex)
                {
                    Log($"Could not remove {file} from the backup: {ex.Message}");
                }
            }

            return removed;
        }

        public static void StartFilesBackup(string[] filesToBackup)
        {
            var handlerInstance = GenericGameHandler.Instance;
            string gameGUID = handlerInstance.CurrentGameInfo.GUID;
            string gameContentPath = GameManager.Instance.GetGameContentPath(gameGUID) /* PlayOut Co-Op */;
            var players = handlerInstance.profile.DevicesList;
            Log("Start processing files backup");
            int copied = 0;
            int removed = 0;
            for (int i = 0; i < players.Count; i++)
            {
                try
                {
                    string instance = InstanceFolder(gameContentPath, i);
                    if (!Directory.Exists(instance))
                    {
                        continue;
                    }

                    if (!UsableNickname(players[i].Nickname))
                    {
                        Log($"Player {i + 1}: nickname '{players[i].Nickname}' cannot name a backup folder, files not backed up");
                        continue;
                    }

                    string destRoot = PlayerBackupFolder(gameGUID, players[i].Nickname);
                    foreach (string file in filesToBackup)
                    {
                        string relative = Relative(file);
                        string source = Path.Combine(instance, relative);
                        if (!File.Exists(source))
                        {
                            Log($"No file to back up at {source}");
                            removed += PruneDeleted(destRoot, instance, relative);
                            continue;
                        }

                        if (CopyFile(source, Path.Combine(destRoot, relative)))
                        {
                            copied++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Files backup failed for player {i + 1}: {ex.Message}");
                }
            }

            Log($"Files backup finished: {copied} file(s), {removed} removed");
        }

        public static void StartFoldersBackup(string[] foldersToBackup)
        {
            var handlerInstance = GenericGameHandler.Instance;
            string gameGUID = handlerInstance.CurrentGameInfo.GUID;
            string gameContentPath = GameManager.Instance.GetGameContentPath(gameGUID) /* PlayOut Co-Op */;
            var players = handlerInstance.profile.DevicesList;
            Log("Start processing folders backup");
            int copied = 0;
            int removed = 0;
            for (int i = 0; i < players.Count; i++)
            {
                try
                {
                    string instance = InstanceFolder(gameContentPath, i);
                    if (!Directory.Exists(instance))
                    {
                        continue;
                    }

                    if (!UsableNickname(players[i].Nickname))
                    {
                        Log($"Player {i + 1}: nickname '{players[i].Nickname}' cannot name a backup folder, folders not backed up");
                        continue;
                    }

                    string destRoot = PlayerBackupFolder(gameGUID, players[i].Nickname);
                    foreach (string folder in foldersToBackup)
                    {
                        string relative = Relative(folder);
                        string source = Path.Combine(instance, relative);
                        if (File.Exists(source))
                        {
                            if (CopyFile(source, Path.Combine(destRoot, relative)))
                            {
                                copied++;
                            }

                            continue;
                        }

                        if (!Directory.Exists(source))
                        {
                            Log($"No folder to back up at {source}");
                            removed += PruneDeleted(destRoot, instance, relative);
                            continue;
                        }

                        string[] files;
                        try
                        {
                            files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
                        }
                        catch (Exception ex)
                        {
                            Log($"Could not list {source}: {ex.Message}");
                            continue;
                        }

                        foreach (string file in files)
                        {
                            string fileRelative = file.Substring(instance.Length).TrimStart('\\');
                            if (CopyFile(file, Path.Combine(destRoot, fileRelative)))
                            {
                                copied++;
                            }
                        }

                        removed += PruneDeleted(destRoot, instance, relative);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Folders backup failed for player {i + 1}: {ex.Message}");
                }
            }

            Log($"Folders backup finished: {copied} file(s), {removed} removed");
        }

        #region Restore folders backups

        public static void StartBackupsRestoration()
        {
            lock (presentLock)
            {
                presentAfterRestore.Clear();
            }

            try
            {
                RestoreAll();
            }
            catch (Exception ex)
            {
                Log($"Backups restoration failed: {ex.Message}");
            }
        }

        private static void RestoreAll()
        {
            var handlerInstance = GenericGameHandler.Instance;
            string gameGUID = handlerInstance.CurrentGameInfo.GUID;
            string gameContentPath = GameManager.Instance.GetGameContentPath(gameGUID) /* PlayOut Co-Op */;
            var players = handlerInstance.profile.DevicesList;

            if (!Directory.Exists(gameContentPath))
            {
                Log($"Directory {gameContentPath} not found");
                return;
            }

            if (!Directory.Exists(Path.Combine(BackupDirectory, gameGUID)))
            {
                Log("No backed up data to restore");
                return;
            }

            Log("Start processing backups restoration");
            for (int i = 0; i < players.Count; i++)
            {
                if (!UsableNickname(players[i].Nickname))
                {
                    Log($"Player {i + 1}: nickname '{players[i].Nickname}' cannot name a backup folder, nothing restored");
                    continue;
                }

                string sourceRoot = PlayerBackupFolder(gameGUID, players[i].Nickname);
                string instance = InstanceFolder(gameContentPath, i);
                if (!Directory.Exists(sourceRoot) || !Directory.Exists(instance))
                {
                    Log($"Nothing to restore for {players[i].Nickname}");
                    continue;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    Log($"Could not list {sourceRoot}: {ex.Message}");
                    continue;
                }

                int restored = 0;
                foreach (string file in files)
                {
                    try
                    {
                        string relative = file.Substring(sourceRoot.Length).TrimStart('\\');
                        string destination = Path.Combine(instance, relative);
                        // A folder linked to the original game folder (SymlinkFolders) would receive the copy in the
                        // original game: skipped.
                        if (BehindLink(instance, Path.GetDirectoryName(destination)))
                        {
                            Log($"Not restoring {relative} for {players[i].Nickname}: its folder is linked to the game folder");
                            continue;
                        }

                        // An instance file of the player's own that is newer than the backup (the content folder of a
                        // session that ended without its backup) holds the latest progress: kept.
                        bool keep = File.Exists(destination) && !IsLink(destination) &&
                            File.GetLastWriteTimeUtc(destination) > File.GetLastWriteTimeUtc(file) && !IsHardLinked(destination);
                        if (keep)
                        {
                            Log($"Not restoring {relative} for {players[i].Nickname}: the instance copy is newer than the backup");
                        }
                        else if (CopyFile(file, destination))
                        {
                            restored++;
                        }

                        if (File.Exists(destination) && !IsLink(destination))
                        {
                            lock (presentLock)
                            {
                                presentAfterRestore.Add(destination);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Could not restore {file}: {ex.Message}");
                    }
                }

                Log($"Restored {restored} file(s) for {players[i].Nickname}");
            }
        }

        #endregion

        #region Here we backup files/folders specified in the game handler
        public static void ProceedBackup()
        {
            try
            {
                var handlerInstance = GenericGameHandler.Instance;
                var game = handlerInstance.CurrentGameInfo;
                var context = handlerInstance.Context;

                // The context is a copy of the game info (GenericGameInfo.CreateContext copies fields of the same
                // name), so Context.BackupFiles/BackupFolders are usually Game's: each entry is backed up once.
                string[] files = Merge(game.BackupFiles, context?.BackupFiles);
                if (files.Length > 0)
                {
                    StartFilesBackup(files);
                }

                string[] folders = Merge(game.BackupFolders, context?.BackupFolders);
                if (folders.Length > 0)
                {
                    StartFoldersBackup(folders);
                }
            }
            catch (Exception ex)
            {
                Log($"Backup failed: {ex.Message}");
            }
        }

        private static string[] Merge(string[] a, string[] b)
        {
            return (a ?? new string[0]).Concat(b ?? new string[0])
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .GroupBy(p => Relative(p), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToArray();
        }

        #endregion

        private static void Log(string logMessage)
        {
            try
            {
                LogManager.Log("[BACKUP] " + logMessage);
            }
            catch { }
        }
    }
}
