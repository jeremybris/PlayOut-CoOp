// PlayOut Co-Op (GPL-3): the "play" command of PlayOutCoop.exe (CONTRACT.md). Runs one Nucleus co-op session
// headless through Nucleus.Gaming.Headless.HeadlessSession and reports it as JSON lines.
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nucleus.Gaming;
using Nucleus.Gaming.Coop;
using Nucleus.Gaming.Headless;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace PlayOutCoop
{
    internal sealed class Job
    {
        [JsonProperty("handler")] public string Handler;
        [JsonProperty("exePath")] public string ExePath;
        [JsonProperty("screens")] public List<JobScreen> Screens = new List<JobScreen>();
        [JsonProperty("players")] public List<JobPlayer> Players = new List<JobPlayer>();
        [JsonProperty("options")] public Dictionary<string, JToken> Options = new Dictionary<string, JToken>();
        /// <summary>Optional absolute folder for the instance folders: &lt;contentRoot&gt;\&lt;GUID&gt;\Instance&lt;N&gt;.</summary>
        [JsonProperty("contentRoot")] public string ContentRoot;
        /// <summary>Optional: true = link game files through an elevated StartGame.exe like Nucleus (UAC prompt).</summary>
        [JsonProperty("allowElevation")] public bool AllowElevation;
    }

    internal sealed class JobScreen
    {
        [JsonProperty("index")] public int Index;
        [JsonProperty("layout")] public string Layout;
    }

    internal sealed class JobPlayer
    {
        [JsonProperty("nickname")] public string Nickname;
        [JsonProperty("screen")] public int Screen;
        [JsonProperty("slot")] public int Slot;
        [JsonProperty("steamId")] public long? SteamId;
        [JsonProperty("device")] public JobDevice Device;
    }

    internal sealed class JobDevice
    {
        [JsonProperty("kind")] public string Kind;
        [JsonProperty("index")] public int? Index;
        [JsonProperty("guid")] public string Guid;
        [JsonProperty("keyboardHandle")] public string KeyboardHandle;
        [JsonProperty("mouseHandle")] public string MouseHandle;
    }

    internal static class Play
    {
        private static readonly ManualResetEvent StopRequested = new ManualResetEvent(false);
        private static readonly ManualResetEvent SessionEnded = new ManualResetEvent(false);
        private static volatile string stopCause; // "stdin" (stop command), "eof" (stdin closed), "parent" (parent exited), "play-error"
        private static volatile string playError;

        public static int Run(string jobPath, int? parentPid = null)
        {
            if (string.IsNullOrWhiteSpace(jobPath))
            {
                return Program.Fail("--job <path\\to\\job.json> is required.", 2);
            }

            Job job = JsonConvert.DeserializeObject<Job>(File.ReadAllText(Path.GetFullPath(jobPath)));
            if (job == null)
            {
                return Program.Fail("The job file is empty.", 2);
            }

            Validate(job);

            // Listen for "stop" / EOF right away, so PlayOut can cancel even while the handler loads.
            Thread stdinThread = new Thread(ReadStdin) { IsBackground = true, Name = "stdin" };
            stdinThread.Start();

            if (parentPid != null)
            {
                Thread watchdog = new Thread(() => WatchParent(parentPid.Value)) { IsBackground = true, Name = "parent" };
                watchdog.Start();
            }

            GameLinks.AllowElevation = job.AllowElevation;
            if (job.AllowElevation)
            {
                Output.Log("allowElevation: game files are linked through an elevated StartGame.exe (UAC) when the handler links them");
            }

            Output.Emit(new { type = "status", message = "Loading handler " + Path.GetFileName(job.Handler) });
            GenericGameInfo game = EngineHost.LoadHandler(Path.GetFullPath(job.Handler));

            List<string> missing = Preflight.MissingFiles(game);
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The engine root is missing Nucleus files this handler needs: {string.Join(", ", missing)} " +
                    $"(in {EngineHost.EngineRoot}). Re-extract the Nucleus Co-op release (NucleusApp.zip) there.");
            }
            UserGameInfo userGame = EngineHost.CreateUserGame(game, Path.GetFullPath(job.ExePath));
            SetContentRoot(job.ContentRoot, game, Path.GetFullPath(job.ExePath));

            UserScreen[] screens = HeadlessDevices.GetUserScreens();
            foreach (JobScreen js in job.Screens)
            {
                if (js.Index < 0 || js.Index >= screens.Length)
                {
                    throw new ArgumentException($"Screen {js.Index} does not exist (this PC has {screens.Length}).");
                }

                screens[js.Index].Type = ParseLayout(js.Layout);
            }

            List<HeadlessPlayerSlot> slots = new List<HeadlessPlayerSlot>();
            for (int i = 0; i < job.Players.Count; i++)
            {
                JobPlayer jp = job.Players[i];
                if (!job.Screens.Any(s => s.Index == jp.Screen))
                {
                    throw new ArgumentException($"Player {i + 1} uses screen {jp.Screen}, which is not listed in \"screens\".");
                }

                slots.Add(new HeadlessPlayerSlot
                {
                    Player = MakePlayer(jp.Device, i),
                    ScreenIndex = jp.Screen,
                    Slot = jp.Slot,
                    Nickname = string.IsNullOrWhiteSpace(jp.Nickname) ? null : jp.Nickname,
                    SteamID = jp.SteamId,
                });
            }

            Dictionary<string, string> options = job.Options?.ToDictionary(
                kv => kv.Key,
                kv => kv.Value == null || kv.Value.Type == JTokenType.Null ? null :
                      kv.Value.Type == JTokenType.String ? (string)kv.Value : kv.Value.ToString(Formatting.None))
                ?? new Dictionary<string, string>();

            GameProfile profile = GameProfile.CreateHeadless(userGame, screens, slots, options);
            Output.Log("Profile: " + string.Join("; ", profile.DevicesList.Select(p => $"{p.Nickname} pad={p.GamepadId} bounds={p.MonitorBounds}")));

            if (StopRequested.WaitOne(0))
            {
                Output.Emit(new { type = "status", message = "Stopped before launch" });
                Output.Emit(new { type = "ended", reason = "stopped" });
                return 0;
            }

            EngineUI.ProgressSink = (step, index, total) =>
            {
                if (step == "instance")
                {
                    Output.Emit(new { type = "status", message = $"Starting instance {index + 1} of {total}" });
                }

                Output.Emit(new { type = "progress", step, player = index, total });
            };
            EngineUI.Handler = OnEngineUi;

            Output.Emit(new { type = "status", message = $"Starting {game.GameName} for {slots.Count} players" });
            HeadlessSession session = HeadlessSession.Start(userGame, profile);

            session.PlayFinished += err =>
            {
                if (string.IsNullOrEmpty(err))
                {
                    // Not after a stop that arrived during the launch: Play() then returns with no (or killed) instances.
                    List<int> pids = session.Pids;
                    if (!session.HasEnded && !StopRequested.WaitOne(0))
                    {
                        Output.Emit(new { type = "started", pids });
                    }
                }
                else
                {
                    playError = err;
                    Output.Emit(new { type = "error", message = err, fatal = true });
                    if (!session.HasEnded)
                    {
                        stopCause = "play-error";
                        StopRequested.Set();
                    }
                }
            };
            session.Ended += reason => SessionEnded.Set();
            if (session.EndReason != null)
            {
                SessionEnded.Set();
            }

            int which = WaitHandle.WaitAny(new WaitHandle[] { SessionEnded, StopRequested });
            if (which == 1 && !SessionEnded.WaitOne(0))
            {
                Output.Emit(new { type = "status", message = stopCause == "eof" ? "stdin closed, stopping the session" : stopCause == "parent" ? "The parent process exited, stopping the session" : "Stopping the session" });
                NativePrompts.CancelAll(); // a prompt waiting for the player must not hold up the stop
                if (!session.Stop(60000))
                {
                    Output.Emit(new { type = "error", message = "The session did not finish stopping within 60 seconds.", fatal = false });
                }
            }

            string endReason = playError != null ? "error" : (session.EndReason ?? "stopped");
            if (endReason == "error" && playError == null)
            {
                Output.Emit(new { type = "error", message = session.Error ?? "The launch was aborted.", fatal = true });
            }

            NativePrompts.CancelAll();

            // Also covers a Stop() that timed out before Nucleus's End() finished (idempotent).
            GameDpiAwareness.RestoreAll();
            Nucleus.Gaming.Tools.GameStarter.StartGameUtil.RestoreGameFileAttributes();

            Output.Emit(new { type = "ended", reason = endReason });
            return endReason == "error" ? 1 : 0;
        }

        private static void ReadStdin()
        {
            try
            {
                TextReader input = new StreamReader(Console.OpenStandardInput(), new System.Text.UTF8Encoding(false));
                string line;
                while ((line = input.ReadLine()) != null)
                {
                    string cmd = line.Trim().ToLowerInvariant();
                    if (cmd.Length == 0)
                    {
                        continue;
                    }

                    if (cmd == "stop")
                    {
                        stopCause = "stdin";
                        StopRequested.Set();
                        return;
                    }

                    Output.Emit(new { type = "error", message = $"Unknown command '{line.Trim()}' (expected: stop).", fatal = false });
                }
            }
            catch (Exception ex)
            {
                Output.Log("stdin read failed: " + ex.Message);
            }

            stopCause = "eof";
            StopRequested.Set();
        }

        /// <summary>
        /// Every Nucleus dialog/prompt. With --prompts native the ones that wait for the player are shown as real
        /// dialogs ("prompt", then "promptAnswered"). Everything else, and everything with --prompts auto: reported as a
        /// "ui" event and answered with its default (as Enter would).
        /// </summary>
        private static EngineUiResponse OnEngineUi(EngineUiRequest req)
        {
            if (req.Kind == EngineUiKind.Osd)
            {
                Output.Emit(new { type = "status", message = req.Message, osd = true });
                return null;
            }

            if (NativePrompts.Enabled && NativePrompts.IsWaiting(req) && !NativePrompts.Cancelled && !StopRequested.WaitOne(0))
            {
                string kind = NativePrompts.KindOf(req);
                Output.Emit(new
                {
                    type = "prompt",
                    kind,
                    title = NativePrompts.TitleOf(req),
                    message = req.Message,
                    buttons = req.Kind == EngineUiKind.MessageBox ? req.Buttons.ToString() : null,
                    source = req.Source,
                });
                EngineUiResponse resp = NativePrompts.Show(req);
                Output.Emit(new
                {
                    type = "promptAnswered",
                    kind,
                    answer = (resp?.Result ?? req.DefaultResult).ToString().ToLowerInvariant(),
                    cancelled = NativePrompts.Cancelled,
                });
                return resp;
            }

            Output.Emit(new
            {
                type = "ui",
                kind = req.Kind.ToString(),
                title = req.Title,
                message = req.Message,
                icon = req.Kind == EngineUiKind.MessageBox ? req.Icon.ToString() : null,
                answer = req.Kind == EngineUiKind.Prompt ? "continue" : req.DefaultResult.ToString(),
                source = req.Source,
            });
            return null;
        }

        /// <summary>--parent: when that process exits, end the session exactly like a closed stdin.</summary>
        private static void WatchParent(int pid)
        {
            Process parent = null;
            try
            {
                parent = Process.GetProcessById(pid);
                try
                {
                    // A PID reused by a younger process is not our parent.
                    if (parent.StartTime > Process.GetCurrentProcess().StartTime)
                    {
                        Output.Log($"Process {pid} started after this helper: not its parent");
                        parent = null;
                    }
                }
                catch
                {
                    // StartTime needs query access; keep watching the process without the check.
                }
            }
            catch (Exception ex)
            {
                Output.Log($"Parent process {pid} not found ({ex.Message})");
                parent = null;
            }

            if (parent != null)
            {
                try
                {
                    parent.WaitForExit();
                }
                catch (Exception ex)
                {
                    // No SYNCHRONIZE access: poll instead.
                    Output.Log($"Waiting for parent {pid} by polling ({ex.Message})");
                    while (true)
                    {
                        Thread.Sleep(1000);
                        try
                        {
                            Process p = Process.GetProcessById(pid);
                            if (p.HasExited) break;
                        }
                        catch (ArgumentException)
                        {
                            break;
                        }
                        catch
                        {
                            // still running, no access to HasExited
                        }
                    }
                }
            }

            Output.Log($"Parent process {pid} exited");
            if (stopCause == null)
            {
                stopCause = "parent";
            }

            StopRequested.Set();
        }

        /// <summary>job.contentRoot: instance folders go to &lt;contentRoot&gt;\&lt;GUID&gt; instead of &lt;engineRoot&gt;\content\&lt;GUID&gt;.</summary>
        private static void SetContentRoot(string contentRoot, GenericGameInfo game, string exePath)
        {
            GameManager.ContentRootOverride = null;
            if (string.IsNullOrWhiteSpace(contentRoot))
            {
                return;
            }

            string cr = contentRoot.Trim().Replace('/', '\\');
            bool absolute = cr.Length >= 3 && char.IsLetter(cr[0]) && cr[1] == ':' && cr[2] == '\\' || cr.StartsWith(@"\\", StringComparison.Ordinal);
            if (!absolute)
            {
                throw new ArgumentException($"job.contentRoot must be an absolute folder path: '{contentRoot}'.");
            }

            string full = Path.GetFullPath(cr).TrimEnd('\\');
            if (full.Length < 3)
            {
                full += "\\"; // a drive root such as D:\
            }

            // The instance folders must not be inside the game folder Nucleus links or copies (it would link them into
            // themselves), and the game must not be inside the folder the session deletes.
            string gameRoot = GameFolderOf(game, exePath);
            string guidFolder = Path.Combine(full, game.GUID);
            if (IsSameOrUnder(full, gameRoot) || IsSameOrUnder(gameRoot, guidFolder) || IsSameOrUnder(exePath, guidFolder))
            {
                throw new ArgumentException($"job.contentRoot '{full}' overlaps the game folder '{gameRoot}'. Use a folder outside the game folder (on the same drive).");
            }

            Directory.CreateDirectory(full);
            GameManager.ContentRootOverride = full;
            Output.Log($"contentRoot: instance folders in {guidFolder}");
        }

        // Nucleus's own root folder computation (GenericGameHandler.Play): the exe folder minus BinariesFolder.
        private static string GameFolderOf(GenericGameInfo game, string exePath)
        {
            string exeFolder = Path.GetDirectoryName(exePath);
            string root = exeFolder;
            if (!string.IsNullOrEmpty(game.BinariesFolder))
            {
                string bin = game.BinariesFolder.Replace('/', '\\').Trim('\\');
                if (exeFolder.EndsWith("\\" + bin, StringComparison.OrdinalIgnoreCase))
                {
                    root = exeFolder.Substring(0, exeFolder.Length - bin.Length - 1);
                }
                else
                {
                    int i = exeFolder.IndexOf("\\" + bin, StringComparison.OrdinalIgnoreCase);
                    if (i > 0) root = exeFolder.Substring(0, i);
                }
            }

            return Path.GetFullPath(root).TrimEnd('\\');
        }

        private static bool IsSameOrUnder(string path, string folder)
        {
            string p = Path.GetFullPath(path).TrimEnd('\\') + "\\";
            string f = Path.GetFullPath(folder).TrimEnd('\\') + "\\";
            return p.StartsWith(f, StringComparison.OrdinalIgnoreCase);
        }

        private static void Validate(Job job)
        {
            if (string.IsNullOrWhiteSpace(job.Handler)) throw new ArgumentException("job.handler is required.");
            if (string.IsNullOrWhiteSpace(job.ExePath)) throw new ArgumentException("job.exePath is required.");
            if (job.Screens == null || job.Screens.Count == 0) throw new ArgumentException("job.screens must list at least one screen.");
            if (job.Players == null || job.Players.Count == 0) throw new ArgumentException("job.players must list at least one player.");
            if (job.Screens.GroupBy(s => s.Index).Any(g => g.Count() > 1)) throw new ArgumentException("job.screens lists a screen twice.");
        }

        // The UserScreenType names the contract allows (Nucleus's Custom / Manual need its setup-screen editor).
        private static readonly string[] Layouts = { "FullScreen", "DualHorizontal", "DualVertical", "FourPlayers", "SixPlayers", "EightPlayers", "SixteenPlayers" };

        private static UserScreenType ParseLayout(string layout)
        {
            string name = Layouts.FirstOrDefault(l => string.Equals(l, layout?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (name == null)
            {
                throw new ArgumentException($"Unknown layout '{layout}'. Expected one of: {string.Join(", ", Layouts)}.");
            }

            return (UserScreenType)Enum.Parse(typeof(UserScreenType), name);
        }

        private static PlayerInfo MakePlayer(JobDevice d, int playerIndex)
        {
            string kind = d?.Kind?.Trim().ToLowerInvariant() ?? "none";
            switch (kind)
            {
                case "xinput":
                    if (d.Index == null || d.Index < 0 || d.Index > 31)
                    {
                        throw new ArgumentException($"Player {playerIndex + 1}: xinput device needs an index 0-31.");
                    }
                    return HeadlessDevices.CreateXInputPlayer(d.Index.Value);

                case "dinput":
                    if (!Guid.TryParse(d.Guid ?? "", out Guid guid))
                    {
                        throw new ArgumentException($"Player {playerIndex + 1}: dinput device needs its instance guid.");
                    }
                    return HeadlessDevices.CreateDInputPlayer(guid)
                        ?? throw new ArgumentException($"Player {playerIndex + 1}: DirectInput device {d.Guid} is not connected.");

                case "keyboard":
                    IntPtr? kb = ParseHandle(d.KeyboardHandle, playerIndex);
                    IntPtr? mouse = ParseHandle(d.MouseHandle, playerIndex);
                    bool sharedKb = kb == null || kb == IntPtr.Zero;
                    bool sharedMouse = mouse == null || mouse == IntPtr.Zero;
                    if (sharedKb && sharedMouse)
                    {
                        return HeadlessDevices.CreateSharedKeyboardPlayer();
                    }
                    return HeadlessDevices.CreateRawKeyboardMousePlayer(kb ?? (IntPtr)(-1), mouse ?? (IntPtr)(-1));

                case "none":
                    return HeadlessDevices.CreateNoInputPlayer();

                default:
                    throw new ArgumentException($"Player {playerIndex + 1}: unknown device kind '{d.Kind}' (xinput, dinput, keyboard or none).");
            }
        }

        private static IntPtr? ParseHandle(string s, int playerIndex)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            string t = s.Trim();
            bool hex = t.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (hex ? long.TryParse(t.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long v)
                    : long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
            {
                return new IntPtr(v);
            }

            throw new ArgumentException($"Player {playerIndex + 1}: bad device handle '{s}'.");
        }
    }
}
