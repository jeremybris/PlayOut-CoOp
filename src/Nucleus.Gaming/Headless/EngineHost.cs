// PlayOut Co-Op - headless front for the Nucleus Co-op engine.
// Copyright (C) 2026 PlayOut Co-Op contributors. Derived from Nucleus Co-op (SplitScreen-Me), GPL-3.
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. See LICENSE.

using Nucleus.Gaming.App.Settings;
using Nucleus.Gaming.Cache;
using Nucleus.Gaming.Coop;
using Nucleus.Gaming.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Nucleus.Gaming.Headless
{
    public sealed class EngineHostOptions
    {
        /// <summary>Do what NucleusCoop.exe's Program.Main does for DPI (DPIManager.PreInitialize + per-monitor aware).</summary>
        public bool SetDpiAwareness = true;
        /// <summary>Make the engine root the current directory (Nucleus assumes cwd == install root: debug-log.txt, Nucleus.IJ's Settings.ini).</summary>
        public bool SetCurrentDirectory = true;
        /// <summary>Show Nucleus's split-division background windows (Game.HideDesktop / SplitDiv) while headless.</summary>
        public bool AllowBackgroundWindows = true;
        /// <summary>Keys written into a newly created Settings.ini on top of Nucleus's defaults: "Section/Key" = value.</summary>
        public Dictionary<string, string> SettingsOverrides = new Dictionary<string, string>
        {
            // PlayOut owns layouts; do not let Nucleus write "game profiles\<GUID>\Profile[N].json" on every launch.
            { "Misc/DisableGameProfiles", "True" },
            { "Misc/IgnoreInputLockReminder", "True" },
        };
    }

    /// <summary>
    /// One-time setup of the Nucleus engine for a headless host. Call <see cref="Initialize"/> first, on the thread
    /// that will drive the engine (any thread; it does not need a message loop).
    /// </summary>
    public static class EngineHost
    {
        public static bool Initialized { get; private set; }
        public static string EngineRoot => Globals.NucleusInstallRoot;

        /// <summary>
        /// Points the engine at <paramref name="engineRoot"/> (Globals.NucleusInstallRoot), writes a default
        /// Settings.ini there if missing, switches every Nucleus dialog to <see cref="EngineUI"/>, loads the settings
        /// and player identity cache like NucleusCoop's Program.Main, and creates the GameManager (without scanning
        /// handlers\). Safe to call once per process.
        /// </summary>
        public static void Initialize(string engineRoot, EngineHostOptions options = null)
        {
            if (Initialized)
            {
                return;
            }

            options = options ?? new EngineHostOptions();

            if (string.IsNullOrEmpty(engineRoot))
            {
                throw new ArgumentException("engineRoot is required", nameof(engineRoot));
            }

            engineRoot = Path.GetFullPath(engineRoot);
            Directory.CreateDirectory(engineRoot);
            Directory.CreateDirectory(Path.Combine(engineRoot, "handlers"));
            Directory.CreateDirectory(Path.Combine(engineRoot, "content"));
            Directory.CreateDirectory(Path.Combine(engineRoot, "utils", "backup"));

            Globals.NucleusInstallRoot = engineRoot;

            if (options.SetCurrentDirectory)
            {
                Environment.CurrentDirectory = engineRoot;
            }

            EngineUI.Headless = true;
            EngineUI.AllowBackgroundWindows = options.AllowBackgroundWindows;

            EnsureSettingsIni(options.SettingsOverrides);

            if (options.SetDpiAwareness)
            {
                DPIManager.PreInitialize();
                User32Util.SetProcessDpiAwareness(ProcessDPIAwareness.ProcessDpiUnaware); // Nucleus: always per-monitor aware
            }

            if (!App_Settings_Loader.InitializeSettings())
            {
                throw new InvalidOperationException("Nucleus could not load " + Path.Combine(engineRoot, "Settings.ini"));
            }

            PlayersIdentityCache.LoadPlayersIdentityCache();

            _ = LogManager.Instance; // content\app.log; GenericGameHandler registers log callbacks on it

            if (GameManager.Instance == null)
            {
                new GameManager(false);
            }

            Initialized = true;
            EngineUI.Log("HOST", $"Engine root: {engineRoot}");
        }

        /// <summary>Writes Nucleus's default Settings.ini (from NucleusCoopTool/copyoutput) if the root has none.</summary>
        public static void EnsureSettingsIni(IDictionary<string, string> overrides)
        {
            string path = Path.Combine(Globals.NucleusInstallRoot, "Settings.ini");
            if (File.Exists(path))
            {
                return;
            }

            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Nucleus.Gaming.Headless.DefaultSettings.ini"))
            using (StreamReader r = new StreamReader(s))
            {
                File.WriteAllText(path, r.ReadToEnd());
            }

            if (overrides != null)
            {
                IniFile ini = new IniFile(path);
                foreach (KeyValuePair<string, string> kv in overrides)
                {
                    string[] parts = kv.Key.Split('/');
                    ini.IniWriteValue(parts[0], parts[1], kv.Value);
                }
            }
        }

        /// <summary>
        /// Loads one handler script (handlers\&lt;GameName&gt;.js) with Jint exactly as Nucleus does. Throws with the
        /// script error when the handler fails to run.
        /// </summary>
        public static GenericGameInfo LoadHandler(string handlerPath)
        {
            RequireInit();

            if (!File.Exists(handlerPath))
            {
                throw new FileNotFoundException("Handler not found", handlerPath);
            }

            string handlersDir = Path.GetFullPath(Path.Combine(EngineRoot, "handlers")).TrimEnd('\\');
            string dir = Path.GetDirectoryName(Path.GetFullPath(handlerPath)).TrimEnd('\\');
            if (!string.Equals(dir, handlersDir, StringComparison.OrdinalIgnoreCase))
            {
                EngineUI.Log("HOST", $"Warning: handler is not in {handlersDir}; Nucleus resolves the handler's files there.");
            }

            GenericGameInfo info = GameManager.Instance.LoadHandlerFile(handlerPath);
            if (info.GUID == null)
            {
                string err = info.LoadError ?? "no Game.GUID set";
                // GenericGameInfo pads every line with blank lines, so Jint's line numbers are doubled (Nucleus halves them too).
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(err, @"^Line (\d+):");
                if (m.Success)
                {
                    err = $"Line {int.Parse(m.Groups[1].Value) / 2}:" + err.Substring(m.Length);
                }

                throw new InvalidOperationException($"Handler {Path.GetFileName(handlerPath)} failed: {err}");
            }

            return info;
        }

        /// <summary>The UserGameInfo Nucleus keeps for a game added with that exe (GameManager.AddGame, without saving it).</summary>
        public static UserGameInfo CreateUserGame(GenericGameInfo game, string exePath)
        {
            RequireInit();

            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException("Game executable not found", exePath);
            }

            UserGameInfo userGame = new UserGameInfo();
            userGame.InitializeDefault(game, exePath);
            return userGame;
        }

        private static void RequireInit()
        {
            if (!Initialized)
            {
                throw new InvalidOperationException("Call EngineHost.Initialize(engineRoot) first.");
            }
        }
    }
}
