// PlayOut Co-Op engine smoke test (GPL-3). Exercises the headless entry points; not the shipped CLI.
using Newtonsoft.Json;
using Nucleus.Gaming;
using Nucleus.Gaming.Coop;
using Nucleus.Gaming.Headless;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace EngineSmoke
{
    internal static class Program
    {
        private static TextWriter stdout;
        private static readonly object outLock = new object();

        private static void Emit(object o)
        {
            lock (outLock)
            {
                stdout.WriteLine(JsonConvert.SerializeObject(o));
                stdout.Flush();
            }
        }

        private static string Arg(string[] args, string name, string def = null)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : def;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            // Nucleus writes to Console.Out in many places: keep stdout for JSON only.
            stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
            Console.SetOut(Console.Error);

            try
            {
                string root = Arg(args, "--root") ?? throw new ArgumentException("--root required");
                EngineUI.LogSink = (cat, msg) => Console.Error.WriteLine($"[{cat}] {msg}");
                EngineHost.Initialize(root);

                switch (args[0])
                {
                    case "devices": return Devices();
                    case "inspect": return Inspect(Arg(args, "--handler"));
                    case "split": return Split(Arg(args, "--layout", "FourPlayers"), int.Parse(Arg(args, "--screen", "0")));
                    case "play": return Play(args);
                    default: throw new ArgumentException("unknown command " + args[0]);
                }
            }
            catch (Exception ex)
            {
                Emit(new { type = "error", message = ex.Message, fatal = true });
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static int Devices()
        {
            var screens = HeadlessDevices.GetScreens();
            var x = HeadlessDevices.GetXInputPads(4);
            var d = HeadlessDevices.GetDInputPads();
            var raw = HeadlessDevices.GetRawKeyboardsAndMice();
            Emit(new
            {
                type = "devices",
                gamepads = x.Select(p => (object)new { id = "xinput:" + p.Index, kind = "xinput", index = p.Index, connected = p.Connected, api = p.Api, vid = p.VendorId, pid = p.ProductId })
                    .Concat(d.Where(p => !p.IsXInputDevice).Select(p => (object)new { id = "dinput:" + p.InstanceGuid.ToString("B"), kind = "dinput", index = p.Index, name = p.Name, guid = p.InstanceGuid.ToString("B") })),
                keyboards = raw.Where(r => r.IsKeyboard).Select(r => new { handle = "0x" + r.Handle.ToString("X"), name = r.Name }),
                mice = raw.Where(r => r.IsMouse).Select(r => new { handle = "0x" + r.Handle.ToString("X"), name = r.Name }),
                screens = screens.Select(s => new { index = s.Index, primary = s.Primary, bounds = new { x = s.Bounds.X, y = s.Bounds.Y, width = s.Bounds.Width, height = s.Bounds.Height }, name = s.DeviceName }),
                dinputAll = d.Count,
            });
            return 0;
        }

        private static int Inspect(string handler)
        {
            GenericGameInfo g = EngineHost.LoadHandler(handler);
            HeadlessDevices.DeviceKindsFor(g, out bool xi, out bool di, out bool sdl, out bool kb, out bool raw);
            Emit(new
            {
                type = "handler",
                gameName = g.GameName,
                guid = g.GUID,
                exeName = g.ExecutableName,
                exeNames = g.ExecutableNames,
                steamId = g.SteamID,
                maxPlayers = g.MaxPlayers,
                maxPlayersOneMonitor = g.MaxPlayersOneMonitor,
                supportsKeyboard = g.SupportsKeyboard,
                supportsMultipleKeyboardsAndMice = g.SupportsMultipleKeyboardsAndMice,
                needsSteamClient = g.NeedSteamClient,
                binariesFolder = g.BinariesFolder,
                hubId = g.Hub?.Handler?.Id,
                hubVersion = g.Hub?.Handler?.Version,
                usesXInput = xi,
                usesDInput = di,
                options = g.Options.Select(HeadlessOptions.Describe),
                description = g.Description,
            });
            return 0;
        }

        private static int Split(string layout, int screenIndex)
        {
            UserScreen[] screens = HeadlessDevices.GetUserScreens();
            screens[screenIndex].Type = (UserScreenType)Enum.Parse(typeof(UserScreenType), layout);
            Emit(new { type = "split", layout, screen = screenIndex, slots = GameProfile.GetSplit(screens[screenIndex]).Select(r => new { r.X, r.Y, r.Width, r.Height }) });
            return 0;
        }

        // play --handler H --exe E --layout L --screen 0 --players xinput:0,none,kb --timeout 60 --opt key=value
        private static int Play(string[] args)
        {
            GenericGameInfo g = EngineHost.LoadHandler(Arg(args, "--handler"));
            UserGameInfo ug = EngineHost.CreateUserGame(g, Arg(args, "--exe"));
            int screenIndex = int.Parse(Arg(args, "--screen", "0"));
            UserScreen[] screens = HeadlessDevices.GetUserScreens();
            screens[screenIndex].Type = (UserScreenType)Enum.Parse(typeof(UserScreenType), Arg(args, "--layout", "DualVertical"));

            List<HeadlessPlayerSlot> players = new List<HeadlessPlayerSlot>();
            string[] devs = Arg(args, "--players", "none,none").Split(',');
            for (int i = 0; i < devs.Length; i++)
            {
                string dv = devs[i];
                PlayerInfo p;
                if (dv.StartsWith("xinput:")) p = HeadlessDevices.CreateXInputPlayer(int.Parse(dv.Substring(7)));
                else if (dv == "kb") p = HeadlessDevices.CreateSharedKeyboardPlayer();
                else p = HeadlessDevices.CreateNoInputPlayer();
                players.Add(new HeadlessPlayerSlot { Player = p, ScreenIndex = screenIndex, Slot = i, Nickname = "Tester" + (i + 1) });
            }

            Dictionary<string, string> opts = new Dictionary<string, string>();
            string o = Arg(args, "--opt");
            if (o != null) opts[o.Split('=')[0]] = o.Split('=')[1];

            GameProfile profile = GameProfile.CreateHeadless(ug, screens, players, opts);
            Emit(new { type = "status", message = "profile", players = profile.DevicesList.Select(p => new { p.Nickname, p.GamepadId, bounds = p.MonitorBounds.ToString(), p.IsXInput, p.IsKeyboardPlayer }) });

            EngineUI.ProgressSink = (step, i, total) => Emit(new { type = "progress", step, player = i, total });
            EngineUI.Handler = req => { Emit(new { type = "ui", kind = req.Kind.ToString(), title = req.Title, message = req.Message, source = req.Source }); return null; };

            HeadlessSession session = HeadlessSession.Start(ug, profile);
            session.PlayFinished += err =>
            {
                if (string.IsNullOrEmpty(err)) Emit(new { type = "started", pids = session.Pids });
                else Emit(new { type = "error", message = err, fatal = true });
            };

            int timeout = int.Parse(Arg(args, "--timeout", "60"));
            Thread stdinThread = new Thread(() =>
            {
                string line;
                while ((line = Console.In.ReadLine()) != null)
                {
                    if (line.Trim() == "stop") break;
                }
                Emit(new { type = "status", message = "stop requested" });
                session.Stop();
            }) { IsBackground = true };
            stdinThread.Start();

            if (!session.WaitForEnd(timeout * 1000))
            {
                Emit(new { type = "status", message = "timeout, stopping" });
                session.Stop();
            }

            Emit(new { type = "ended", reason = session.EndReason ?? "stopped", pids = session.Pids });
            stdout.Flush();
            Environment.Exit(0); // Nucleus leaves foreground dispatcher threads running
            return 0;
        }
    }
}
