// PlayOut Co-Op - headless front for the Nucleus Co-op engine.
// Copyright (C) 2026 PlayOut Co-Op contributors. Derived from Nucleus Co-op (SplitScreen-Me), GPL-3.
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. See LICENSE.

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Nucleus.Gaming.Headless
{
    /// <summary>How one file or folder of an instance folder was made.</summary>
    public enum LinkMethod
    {
        /// <summary>A symbolic link (unprivileged with Developer Mode, or elevated).</summary>
        Symlink,
        /// <summary>An NTFS directory junction (folders, when symbolic links need a privilege we do not have).</summary>
        Junction,
        /// <summary>A hard link (files on the same volume).</summary>
        HardLink,
        /// <summary>A plain copy (files on another volume, or where a hard link is not possible).</summary>
        Copy,
        /// <summary>Something already exists at the link path (left as it is, as Nucleus's CreateSymbolicLink did).</summary>
        Exists,
        /// <summary>Nothing could be made.</summary>
        Failed,
    }

    /// <summary>
    /// Instance-folder links without UAC. Nucleus's SymlinkGame needs SeCreateSymbolicLinkPrivilege (an elevated
    /// StartGame.exe). Here every link is first tried as an unprivileged symbolic link
    /// (SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE, works with Windows Developer Mode); when that is refused, folders
    /// become directory junctions and files become hard links (same volume) or copies. Also: a cleanup that never
    /// follows a link or junction into the original game folder.
    /// </summary>
    public static class GameLinks
    {
        /// <summary>
        /// True = do what Nucleus did: link through an elevated StartGame.exe ("runas", a UAC prompt). Set from the
        /// job's "allowElevation". Default false: link in-process, never elevate.
        /// </summary>
        public static bool AllowElevation;

        /// <summary>Test switch: behave as if symbolic links were not allowed (env PLAYOUTCOOP_NO_SYMLINKS=1).</summary>
        public static bool ForceNoSymlinks = Environment.GetEnvironmentVariable("PLAYOUTCOOP_NO_SYMLINKS") == "1";

        private const int SYMBOLIC_LINK_FLAG_DIRECTORY = 0x1;
        private const int SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE = 0x2;
        private const int ERROR_INVALID_PARAMETER = 87;
        private const int ERROR_PRIVILEGE_NOT_HELD = 1314;
        private const int ERROR_ALREADY_EXISTS = 183;
        private const int ERROR_FILE_EXISTS = 80;

        // Once a symbolic link has been refused for lack of privilege, do not ask again for every file.
        private static volatile bool symlinksRefused;
        private static volatile bool unprivilegedFlagUnsupported;

        private static readonly int[] counts = new int[Enum.GetValues(typeof(LinkMethod)).Length];
        private static readonly object failLock = new object();
        private static readonly List<string> failures = new List<string>();

        private static void Log(string message)
        {
            EngineUI.Log("LINK", message);
        }

        private static LinkMethod Count(LinkMethod m)
        {
            Interlocked.Increment(ref counts[(int)m]);
            return m;
        }

        private static void Fail(string what)
        {
            lock (failLock)
            {
                if (failures.Count < 20)
                {
                    failures.Add(what);
                    Log("failed: " + what);
                }
            }
        }

        public static void ResetCounters()
        {
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = 0;
            }

            lock (failLock)
            {
                failures.Clear();
            }
        }

        /// <summary>"12 symlink, 3 hardlink, ..." for the methods used since <see cref="ResetCounters"/>.</summary>
        public static string Summary()
        {
            StringBuilder sb = new StringBuilder();
            foreach (LinkMethod m in Enum.GetValues(typeof(LinkMethod)))
            {
                int n = counts[(int)m];
                if (n > 0)
                {
                    sb.Append(sb.Length > 0 ? ", " : "").Append(n).Append(' ').Append(m.ToString().ToLowerInvariant());
                }
            }

            return sb.Length > 0 ? sb.ToString() : "nothing linked";
        }

        public static int CountOf(LinkMethod m) => counts[(int)m];

        private static bool Exists(string path)
        {
            // GetFileAttributes does not follow links: a dangling link counts as existing, like CreateSymbolicLink sees it.
            return GetFileAttributesW(path) != INVALID_FILE_ATTRIBUTES;
        }

        /// <summary>
        /// Tries an unprivileged symbolic link. Returns true on success; otherwise lastError holds the Win32 error.
        /// </summary>
        private static bool TrySymlink(string link, string target, bool directory, out int lastError)
        {
            lastError = ERROR_PRIVILEGE_NOT_HELD;
            if (ForceNoSymlinks && !symlinksRefused)
            {
                symlinksRefused = true;
                Log("Symbolic links disabled (PLAYOUTCOOP_NO_SYMLINKS=1); using junctions for folders and hard links (or copies) for files instead.");
            }

            if (symlinksRefused)
            {
                return false;
            }

            int flags = directory ? SYMBOLIC_LINK_FLAG_DIRECTORY : 0;
            if (!unprivilegedFlagUnsupported)
            {
                if (CreateSymbolicLinkW(link, target, flags | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE))
                {
                    return true;
                }

                lastError = Marshal.GetLastWin32Error();
                if (lastError == ERROR_INVALID_PARAMETER)
                {
                    // Windows before 10 1703 does not know the flag.
                    unprivilegedFlagUnsupported = true;
                }
            }

            if (unprivilegedFlagUnsupported)
            {
                if (CreateSymbolicLinkW(link, target, flags))
                {
                    return true;
                }

                lastError = Marshal.GetLastWin32Error();
            }

            if (lastError == ERROR_PRIVILEGE_NOT_HELD && !symlinksRefused)
            {
                symlinksRefused = true;
                Log("Symbolic links need Developer Mode or elevation here; using junctions for folders and hard links (or copies) for files instead.");
            }

            return false;
        }

        /// <summary>
        /// Makes <paramref name="link"/> point at the file <paramref name="target"/>. hardLink = Nucleus's HardlinkGame
        /// (hard link first); otherwise a symbolic link, falling back to a hard link, then a copy.
        /// </summary>
        public static LinkMethod LinkFile(string link, string target, bool hardLink)
        {
            if (Exists(link))
            {
                return Count(LinkMethod.Exists);
            }

            if (!hardLink)
            {
                if (TrySymlink(link, target, false, out int err))
                {
                    return Count(LinkMethod.Symlink);
                }

                if (err == ERROR_ALREADY_EXISTS || err == ERROR_FILE_EXISTS)
                {
                    return Count(LinkMethod.Exists);
                }
            }

            if (CreateHardLinkW(link, target, IntPtr.Zero))
            {
                return Count(LinkMethod.HardLink);
            }

            int hlErr = Marshal.GetLastWin32Error();
            if (hlErr == ERROR_ALREADY_EXISTS || hlErr == ERROR_FILE_EXISTS)
            {
                return Count(LinkMethod.Exists);
            }

            try
            {
                // Different volume (ERROR_NOT_SAME_DEVICE), FAT/exFAT, or no access to the original: copy it.
                File.Copy(target, link, false);
                return Count(LinkMethod.Copy);
            }
            catch (Exception ex)
            {
                Fail($"{link} -> {target}: hard link error {hlErr}, copy: {ex.Message}");
                return Count(LinkMethod.Failed);
            }
        }

        /// <summary>
        /// Makes the folder <paramref name="link"/> point at <paramref name="target"/>: a symbolic link, or a junction
        /// when symbolic links are not allowed. Failed = nothing was made (the caller then creates a real folder).
        /// </summary>
        public static LinkMethod LinkDirectory(string link, string target)
        {
            if (Exists(link))
            {
                return Count(LinkMethod.Exists);
            }

            if (TrySymlink(link, target, true, out int err))
            {
                return Count(LinkMethod.Symlink);
            }

            if (err == ERROR_ALREADY_EXISTS || err == ERROR_FILE_EXISTS)
            {
                return Count(LinkMethod.Exists);
            }

            try
            {
                CreateJunction(link, target);
                return Count(LinkMethod.Junction);
            }
            catch (Exception ex)
            {
                Fail($"{link} -> {target}: junction: {ex.Message}");
                return Count(LinkMethod.Failed);
            }
        }

        /// <summary>True if the path is a symbolic link, junction or other reparse point (the link itself, not its target).</summary>
        public static bool IsReparsePoint(string path)
        {
            uint a = GetFileAttributesW(path);
            return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_REPARSE_POINT) != 0;
        }

        // ---- junctions -------------------------------------------------------------------------------------------

        private const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;
        private const uint FSCTL_SET_REPARSE_POINT = 0x000900A4;

        /// <summary>Creates an NTFS directory junction (mount point) at link pointing to the absolute local folder target.</summary>
        public static void CreateJunction(string link, string target)
        {
            string full = Path.GetFullPath(target).TrimEnd('\\');
            if (full.StartsWith(@"\\", StringComparison.Ordinal))
            {
                throw new IOException("Junctions cannot point at network paths.");
            }

            if (full.Length == 2 && full[1] == ':')
            {
                full += "\\";
            }

            string substitute = @"\??\" + full;
            byte[] subBytes = Encoding.Unicode.GetBytes(substitute);
            byte[] printBytes = Encoding.Unicode.GetBytes(full);

            // REPARSE_DATA_BUFFER (MountPointReparseBuffer)
            int pathBufferLength = subBytes.Length + 2 + printBytes.Length + 2;
            int dataLength = 8 + pathBufferLength;
            byte[] buffer = new byte[8 + dataLength];
            using (MemoryStream ms = new MemoryStream(buffer))
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(IO_REPARSE_TAG_MOUNT_POINT);
                w.Write((ushort)dataLength);
                w.Write((ushort)0);
                w.Write((ushort)0);                          // SubstituteNameOffset
                w.Write((ushort)subBytes.Length);            // SubstituteNameLength
                w.Write((ushort)(subBytes.Length + 2));      // PrintNameOffset
                w.Write((ushort)printBytes.Length);          // PrintNameLength
                w.Write(subBytes);
                w.Write((ushort)0);
                w.Write(printBytes);
                w.Write((ushort)0);
            }

            Directory.CreateDirectory(link);
            bool ok = false;
            try
            {
                using (SafeFileHandle h = CreateFileW(link, GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, IntPtr.Zero,
                    OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero))
                {
                    if (h.IsInvalid)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    if (!DeviceIoControl(h, FSCTL_SET_REPARSE_POINT, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }

                ok = true;
            }
            finally
            {
                if (!ok)
                {
                    try { RemoveDirectoryW(link); } catch { }
                }
            }
        }

        // ---- safe cleanup ----------------------------------------------------------------------------------------

        /// <summary>
        /// Deletes a folder tree WITHOUT following symbolic links or junctions: a link/junction is removed itself and
        /// never entered, so the original game files they point at are untouched. Read-only hard links are deleted
        /// without clearing the shared read-only attribute of the original file. Throws IOException listing what
        /// could not be deleted.
        /// </summary>
        public static void DeleteTree(string path)
        {
            List<string> errors = new List<string>();
            DeleteEntry(path, errors);
            if (errors.Count > 0)
            {
                throw new IOException($"Could not delete {errors.Count} item(s), first: {errors[0]}");
            }
        }

        private static void DeleteEntry(string path, List<string> errors)
        {
            uint a = GetFileAttributesW(path);
            if (a == INVALID_FILE_ATTRIBUTES)
            {
                return;
            }

            bool isDir = (a & FILE_ATTRIBUTE_DIRECTORY) != 0;
            bool isLink = (a & FILE_ATTRIBUTE_REPARSE_POINT) != 0;

            if (isDir && !isLink)
            {
                List<string> children = new List<string>();
                try
                {
                    // Top level only: the recursion below decides per entry whether it may go deeper.
                    children.AddRange(Directory.EnumerateFileSystemEntries(path));
                }
                catch (Exception ex)
                {
                    errors.Add(path + ": " + ex.Message);
                }

                foreach (string child in children)
                {
                    DeleteEntry(child, errors);
                }
            }

            if (isDir)
            {
                // A directory link or junction: RemoveDirectory removes the link only, never the target's contents.
                if ((a & FILE_ATTRIBUTE_READONLY) != 0 && !isLink)
                {
                    SetFileAttributesW(path, a & ~FILE_ATTRIBUTE_READONLY);
                }

                if (!RemoveDirectoryW(path))
                {
                    errors.Add(path + ": error " + Marshal.GetLastWin32Error());
                }

                return;
            }

            // A file, a file symbolic link (DeleteFile removes the link) or a hard link (removes this name only).
            if (DeleteFileW(path))
            {
                return;
            }

            int err = Marshal.GetLastWin32Error();
            if (DeleteIgnoringReadOnly(path))
            {
                return;
            }

            if ((a & FILE_ATTRIBUTE_READONLY) != 0 && !isLink)
            {
                // Older Windows: clear read-only, delete. For a hard link this also clears it on the original.
                SetFileAttributesW(path, a & ~FILE_ATTRIBUTE_READONLY);
                if (DeleteFileW(path))
                {
                    return;
                }

                err = Marshal.GetLastWin32Error();
            }

            errors.Add(path + ": error " + err);
        }

        private const int FileDispositionInfoEx = 21;
        private const uint FILE_DISPOSITION_FLAG_DELETE = 0x1;
        private const uint FILE_DISPOSITION_FLAG_POSIX_SEMANTICS = 0x2;
        private const uint FILE_DISPOSITION_FLAG_IGNORE_READONLY_ATTRIBUTE = 0x10;

        private static bool DeleteIgnoringReadOnly(string path)
        {
            using (SafeFileHandle h = CreateFileW(path, DELETE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, IntPtr.Zero,
                OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero))
            {
                if (h.IsInvalid)
                {
                    return false;
                }

                uint flags = FILE_DISPOSITION_FLAG_DELETE | FILE_DISPOSITION_FLAG_POSIX_SEMANTICS | FILE_DISPOSITION_FLAG_IGNORE_READONLY_ATTRIBUTE;
                return SetFileInformationByHandle(h, FileDispositionInfoEx, ref flags, 4);
            }
        }

        // ---- interop ---------------------------------------------------------------------------------------------

        private const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;
        private const uint FILE_ATTRIBUTE_READONLY = 0x1;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x400;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint DELETE = 0x00010000;
        private const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, FILE_SHARE_DELETE = 4;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CreateSymbolicLinkW(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFileAttributesW(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetFileAttributesW(string lpFileName, uint dwFileAttributes);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RemoveDirectoryW(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFileW(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
            uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, byte[] lpInBuffer, int nInBufferSize,
            IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle hFile, int fileInformationClass, ref uint info, int bufferSize);
    }
}
