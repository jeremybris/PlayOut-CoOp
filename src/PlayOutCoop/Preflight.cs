// PlayOut Co-Op (GPL-3): checks, before a "play" session starts, that the engine root holds the Nucleus tool files the
// handler will use. Without this a missing file surfaces deep inside Nucleus as "The system cannot find the file
// specified" (or is silently skipped, e.g. a hook DLL), with no hint of which file PlayOut failed to extract.
//
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; see LICENSE.
using Nucleus.Gaming;
using Nucleus.Gaming.Headless;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlayOutCoop
{
    internal static class Preflight
    {
        // Used by every launch: StartGame.exe (unlock/link files, start, mutexes), the injectors and their hook DLLs.
        private static readonly string[] Always =
        {
            "StartGame.exe", "Nucleus.IJx86.exe", "Nucleus.IJx64.exe",
            "Nucleus.Hook32.dll", "Nucleus.Hook64.dll", "Nucleus.SHook32.dll", "Nucleus.SHook64.dll",
            "EasyHook.dll", "EasyHook32.dll", "EasyHook64.dll", "EasyHook32Svc.exe", "EasyHook64Svc.exe",
        };

        private static readonly string[] ProtoInputFiles =
        {
            "ProtoInputLoader32.dll", "ProtoInputLoader64.dll", "ProtoInputHooks32.dll", "ProtoInputHooks64.dll",
            "ProtoInputIJ32.exe", "ProtoInputIJ64.exe", "ProtoInputIJP32.dll", "ProtoInputIJP64.dll",
        };

        /// <summary>Paths (relative to the engine root) the session needs but that are missing. Empty = all present.</summary>
        public static List<string> MissingFiles(GenericGameInfo g)
        {
            string root = EngineHost.EngineRoot;
            List<string> files = new List<string>(Always);
            List<string> dirs = new List<string>();

            var pi = g.ProtoInput;
            if (pi != null && (pi.InjectStartup || pi.InjectRuntime_RemoteLoadMethod || pi.InjectRuntime_EasyHookMethod || pi.InjectRuntime_EasyHookStealthMethod))
            {
                files.AddRange(ProtoInputFiles);
            }

            HeadlessDevices.DeviceKindsFor(g, out _, out _, out bool sdl2, out _, out _);
            if (sdl2) files.Add("SDL2.dll");

            if (g.XInputPlusDll != null && g.XInputPlusDll.Length > 0) dirs.Add(@"utils\XInputPlus");
            if (g.UseGoldberg) dirs.Add(@"utils\GoldbergEmu");
            if (g.UseX360ce) dirs.Add(@"utils\x360ce");
            if (g.UseDevReorder) dirs.Add(@"utils\devreorder");
            if (g.UseDInputBlocker) dirs.Add(@"utils\dinput8.blocker");
            if (g.UseEACBypass) dirs.Add(@"utils\EAC Bypass");
            if (g.UseSteamless) dirs.Add(@"utils\Steamless");
            if (g.UseSteamStubDRMPatcher) dirs.Add(@"utils\Steam Stub DRM Patcher");
            if (g.UseDirectX9Wrapper) dirs.Add(@"utils\DirectXWrapper");
            if (g.UseForceBindIP) dirs.Add(@"utils\ForceBindIP");
            if (g.UseNemirtingasEpicEmu) dirs.Add(@"utils\NemirtingasEpicEmu");
            if (g.UseNemirtingasGalaxyEmu) dirs.Add(@"utils\NemirtingasGalaxyEmu");
            if (!string.IsNullOrEmpty(g.FlawlessWidescreen)) dirs.Add(@"utils\FlawlessWidescreen");

            List<string> missing = files.Where(f => !File.Exists(Path.Combine(root, f))).ToList();
            missing.AddRange(dirs.Where(d => !Directory.Exists(Path.Combine(root, d)) ||
                                             !Directory.EnumerateFileSystemEntries(Path.Combine(root, d)).Any())
                                 .Select(d => d + "\\"));
            return missing;
        }
    }
}
