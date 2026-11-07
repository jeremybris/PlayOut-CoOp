// PlayOut Co-Op (GPL-3): the PlayOutCoop.exe command-line helper. Implements CONTRACT.md on top of the Nucleus Co-op
// engine (src/Nucleus.Gaming, Headless/*). See ENGINE-NOTES.md for the engine calls used here.
//
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; see LICENSE.
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nucleus.Gaming;
using Nucleus.Gaming.Coop;
using Nucleus.Gaming.Headless;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PlayOutCoop
{
    internal static class Program
    {
        private const string Usage =
            "Usage:\n" +
            "  PlayOutCoop.exe devices --root <engineRoot>\n" +
            "  PlayOutCoop.exe inspect --root <engineRoot> --handler <path\\to\\GameName.js>\n" +
            "  PlayOutCoop.exe play    --root <engineRoot> --job <path\\to\\job.json> [--prompts auto|native] [--parent <pid>]";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.None,
        };

        [STAThread]
        private static int Main(string[] args)
        {
            // stdout carries only the JSON lines: Nucleus writes to Console.Out in many places, so Console.Out goes to
            // stderr (and the log) from here on.
            Output.Init();

            string command = args.Length > 0 ? args[0].ToLowerInvariant() : null;
            string root = Arg(args, "--root");

            if (command == null || command == "--help" || command == "-h" || command == "help")
            {
                Console.Error.WriteLine(Usage);
                return Fail("No command given. " + Usage.Replace("\n", " "), 2);
            }

            if (command != "devices" && command != "inspect" && command != "play")
            {
                return Fail($"Unknown command '{args[0]}'. Expected devices, inspect or play.", 2);
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                return Fail("--root <engineRoot> is required.", 2);
            }

            string prompts = (Arg(args, "--prompts") ?? "auto").Trim().ToLowerInvariant();
            if (prompts != "auto" && prompts != "native")
            {
                return Fail($"--prompts must be auto or native, not '{prompts}'.", 2);
            }

            NativePrompts.Enabled = prompts == "native";

            int? parentPid = null;
            string parentArg = Arg(args, "--parent");
            if (parentArg != null)
            {
                if (!int.TryParse(parentArg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid) || pid <= 0)
                {
                    return Fail($"--parent must be a process id, not '{parentArg}'.", 2);
                }

                parentPid = pid;
            }

            int code;
            try
            {
                root = Path.GetFullPath(root);
                Output.OpenLog(root, command);
                Output.Log($"PlayOutCoop {typeof(Program).Assembly.GetName().Version} {string.Join(" ", args.Select(Quote))}");
                Output.Log($"Process: {(Environment.Is64BitProcess ? "64" : "32")}-bit, OS {Environment.OSVersion}");

                EngineUI.LogSink = (category, msg) => Output.Log($"[{category}] {msg}");
                EngineHost.Initialize(root);

                switch (command)
                {
                    case "devices": code = Devices(); break;
                    case "inspect": code = Inspect(Arg(args, "--handler")); break;
                    default: code = Play.Run(Arg(args, "--job"), parentPid); break;
                }
            }
            catch (Exception ex)
            {
                Output.Log("Fatal: " + ex);
                code = Fail(Describe(ex), 1);
                if (command == "play")
                {
                    // PlayOut waits for "ended" to close a session, also when it never launched.
                    Output.Emit(new { type = "ended", reason = "error" });
                }
            }

            Output.Close();
            // Nucleus leaves foreground threads running (dispatchers, timers): returning from Main would not end the process.
            Environment.Exit(code);
            return code;
        }

        internal static int Fail(string message, int code)
        {
            Output.Emit(new { type = "error", message, fatal = true });
            return code;
        }

        internal static string Describe(Exception ex)
        {
            if (ex is System.Reflection.TargetInvocationException && ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            if (ex is FileNotFoundException fnf && !string.IsNullOrEmpty(fnf.FileName))
            {
                return $"{fnf.Message} ({fnf.FileName})";
            }

            return ex.Message;
        }

        internal static string Arg(string[] args, string name)
        {
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static string Quote(string s) => s.IndexOf(' ') >= 0 ? "\"" + s + "\"" : s;

        internal static string Hex(IntPtr handle) => "0x" + handle.ToInt64().ToString("X", CultureInfo.InvariantCulture);

        // ---- devices -----------------------------------------------------------------------------------------

        private static int Devices()
        {
            List<object> gamepads = new List<object>();

            // XInput: slots 0-3 always; slots 4-31 (openxinput) only when something is connected there.
            bool openXInput = File.Exists(Path.Combine(EngineHost.EngineRoot, "openxinput1_3.dll"));
            foreach (HeadlessXInputPad p in HeadlessDevices.GetXInputPads(openXInput ? 32 : 4))
            {
                if (p.Index >= 4 && !p.Connected)
                {
                    continue;
                }

                gamepads.Add(new
                {
                    id = "xinput:" + p.Index,
                    kind = "xinput",
                    index = p.Index,
                    name = p.Connected ? (p.VendorId != 0 ? $"XInput Controller (VID {p.VendorId:X4} PID {p.ProductId:X4})" : "XInput Controller") : $"XInput slot {p.Index + 1} (empty)",
                    // The GUID Nucleus gives XInput players when UseXinputIndex is on.
                    guid = $"00000000-0000-0000-0000-2{(p.Index + 1).ToString("D11", CultureInfo.InvariantCulture)}",
                    connected = p.Connected,
                    vendorId = p.VendorId != 0 ? p.VendorId.ToString("X4") : null,
                    productId = p.ProductId != 0 ? p.ProductId.ToString("X4") : null,
                    api = p.Api,
                });
            }

            try
            {
                foreach (HeadlessDInputPad p in HeadlessDevices.GetDInputPads().Where(d => !d.IsXInputDevice))
                {
                    string guid = p.InstanceGuid.ToString("B");
                    gamepads.Add(new
                    {
                        id = "dinput:" + guid,
                        kind = "dinput",
                        index = -1,
                        name = p.Name,
                        guid,
                        connected = true,
                        productGuid = p.ProductGuid.ToString("B"),
                        dinputIndex = p.Index,
                    });
                }
            }
            catch (Exception ex)
            {
                Output.Log("DirectInput enumeration failed: " + ex);
            }

            List<HeadlessRawDevice> raw = new List<HeadlessRawDevice>();
            try
            {
                raw = HeadlessDevices.GetRawKeyboardsAndMice();
            }
            catch (Exception ex)
            {
                Output.Log("Raw input enumeration failed: " + ex);
            }

            Output.Emit(new
            {
                type = "devices",
                gamepads,
                keyboards = raw.Where(r => r.IsKeyboard).Select(r => new { id = "kb:" + Hex(r.Handle), handle = Hex(r.Handle), name = r.Name }),
                mice = raw.Where(r => r.IsMouse).Select(r => new { id = "mouse:" + Hex(r.Handle), handle = Hex(r.Handle), name = r.Name }),
                screens = HeadlessDevices.GetScreens().Select(s => new
                {
                    index = s.Index,
                    primary = s.Primary,
                    bounds = new { x = s.Bounds.X, y = s.Bounds.Y, width = s.Bounds.Width, height = s.Bounds.Height },
                    name = s.DeviceName,
                }),
            });
            return 0;
        }

        // ---- inspect -----------------------------------------------------------------------------------------

        private static int Inspect(string handler)
        {
            if (string.IsNullOrWhiteSpace(handler))
            {
                return Fail("--handler <path\\to\\GameName.js> is required.", 2);
            }

            GenericGameInfo g = EngineHost.LoadHandler(Path.GetFullPath(handler));
            HeadlessDevices.DeviceKindsFor(g, out bool xinput, out bool dinput, out bool sdl2, out bool keyboard, out bool rawKbm);

            Output.Emit(new
            {
                type = "handler",
                gameName = g.GameName,
                guid = g.GUID,
                exeName = g.ExecutableName,
                exeNames = g.ExecutableNames ?? (string.IsNullOrEmpty(g.ExecutableName) ? new string[0] : new[] { g.ExecutableName }),
                steamId = g.SteamID,
                maxPlayers = g.MaxPlayers,
                maxPlayersOneMonitor = g.MaxPlayersOneMonitor,
                supportsKeyboard = g.SupportsKeyboard,
                supportsMultipleKeyboardsAndMice = g.SupportsMultipleKeyboardsAndMice,
                needsSteamClient = g.NeedSteamClient,
                binariesFolder = g.BinariesFolder,
                hubId = g.Hub?.Handler?.Id,
                hubVersion = g.Hub?.Handler?.Version,
                usesXInput = xinput,
                usesDInput = dinput,
                usesSDL2 = sdl2,
                options = (g.Options ?? new List<GameOption>()).Select(HeadlessOptions.Describe).Select(o => new
                {
                    key = o.Key,
                    name = o.Name,
                    description = o.Description,
                    values = o.Values,
                    @default = o.Default,
                    type = o.Type,
                    hidden = o.Hidden,
                }),
                description = g.Description,
            });
            return 0;
        }
    }

    /// <summary>JSON lines on stdout (thread-safe, UTF-8 without BOM) and the log file / stderr diagnostics.</summary>
    internal static class Output
    {
        private static readonly object OutLock = new object();
        private static readonly object LogLock = new object();
        private static TextWriter stdout;
        private static TextWriter stderr;
        private static StreamWriter logFile;

        public static void Init()
        {
            stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            stderr = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true };

            // Everything the engine prints goes to stderr and the log, never to stdout.
            TextWriter diag = TextWriter.Synchronized(new LogWriter());
            Console.SetOut(diag);
            Console.SetError(diag);
        }

        public static void OpenLog(string root, string command)
        {
            try
            {
                string dir = Path.Combine(root, "logs");
                Directory.CreateDirectory(dir);

                // Keep the 30 newest helper logs.
                foreach (FileInfo old in new DirectoryInfo(dir).GetFiles("playoutcoop-*.log").OrderByDescending(f => f.LastWriteTimeUtc).Skip(29))
                {
                    try { old.Delete(); } catch { }
                }

                string path = Path.Combine(dir, $"playoutcoop-{DateTime.Now:yyyyMMdd-HHmmss}-{command}-{System.Diagnostics.Process.GetCurrentProcess().Id}.log");
                lock (LogLock)
                {
                    logFile = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
                }
            }
            catch (Exception ex)
            {
                stderr.WriteLine("Could not open the log: " + ex.Message);
            }
        }

        public static void Emit(object evt)
        {
            string line = JsonConvert.SerializeObject(evt, Formatting.None);
            lock (OutLock)
            {
                try
                {
                    stdout.WriteLine(line);
                    stdout.Flush();
                }
                catch (IOException)
                {
                    // PlayOut closed our stdout; keep going (the session still has to be ended cleanly).
                }
            }

            lock (LogLock)
            {
                try { logFile?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} OUT {line}"); } catch { }
            }
        }

        public static void Log(string message)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
            lock (LogLock)
            {
                try { stderr?.WriteLine(line); } catch { }
                try { logFile?.WriteLine(line); } catch { }
            }
        }

        public static void Close()
        {
            lock (OutLock)
            {
                try { stdout.Flush(); } catch { }
            }

            lock (LogLock)
            {
                try { logFile?.Flush(); } catch { }
            }
        }

        /// <summary>Collects Console writes from the engine into whole lines for <see cref="Log"/>.</summary>
        private sealed class LogWriter : TextWriter
        {
            private readonly StringBuilder line = new StringBuilder();

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                if (value == '\n')
                {
                    Log(line.ToString().TrimEnd('\r'));
                    line.Clear();
                }
                else
                {
                    line.Append(value);
                }
            }

            public override void Flush()
            {
                if (line.Length > 0)
                {
                    Log(line.ToString());
                    line.Clear();
                }
            }
        }
    }
}
