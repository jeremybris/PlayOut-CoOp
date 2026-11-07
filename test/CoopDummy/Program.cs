// PlayOut Co-Op test game (GPL-3). A tiny window that reports what the co-op engine did to it.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CoopDummy
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Single-instance guard like many games: a named mutex. Nucleus's KillMutex should let more instances start.
            bool createdNew;
            using (var mutex = new Mutex(true, "CoopDummySingleInstance", out createdNew))
            {
                if (!createdNew && Array.IndexOf(args, "--allow-multi") < 0 && File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "single-instance.txt")))
                {
                    MessageBox.Show("CoopDummy is already running.");
                    return;
                }

                Application.EnableVisualStyles();
                Application.Run(new DummyForm(args));
                GC.KeepAlive(mutex);
            }
        }
    }

    internal sealed class DummyForm : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD { public ushort wButtons; public byte bLeftTrigger; public byte bRightTrigger; public short sThumbLX; public short sThumbLY; public short sThumbRX; public short sThumbRY; }
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE { public uint dwPacketNumber; public XINPUT_GAMEPAD Gamepad; }

        // Loaded from the exe folder first, so Nucleus's per-instance proxy xinput1_3.dll (ncoop.ini) applies here.
        [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState13(int index, out XINPUT_STATE state);
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState14(int index, out XINPUT_STATE state);

        private readonly Label label;
        private readonly string[] args;
        private readonly string logPath;
        private bool use13 = true;

        public DummyForm(string[] args)
        {
            this.args = args;
            Text = "CoopDummy " + Process.GetCurrentProcess().Id;
            Width = 640;
            Height = 400;
            BackColor = Color.FromArgb(30, 30, 40);
            label = new Label { Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Consolas", 10f) };
            Controls.Add(label);

            logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "coopdummy-" + Process.GetCurrentProcess().Id + ".log");

            var timer = new System.Windows.Forms.Timer { Interval = 250 };
            timer.Tick += (s, e) => Refresh2();
            timer.Start();
            Refresh2();
        }

        private string Pad(int i)
        {
            XINPUT_STATE st;
            int r;
            try { r = use13 ? XInputGetState13(i, out st) : XInputGetState14(i, out st); }
            catch (DllNotFoundException) { use13 = false; r = XInputGetState14(i, out st); }
            return r == 0 ? $"connected buttons=0x{st.Gamepad.wButtons:X4} LX={st.Gamepad.sThumbLX}" : "empty";
        }

        private void Refresh2()
        {
            string ini = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ncoop.ini");
            string text =
                $"PID      {Process.GetCurrentProcess().Id}\n" +
                $"Exe      {Application.ExecutablePath}\n" +
                $"Args     {string.Join(" ", args)}\n" +
                $"Window   x={Left} y={Top} w={Width} h={Height}  border={FormBorderStyle}\n" +
                $"Client   {ClientSize.Width}x{ClientSize.Height}\n" +
                $"ncoop    {(File.Exists(ini) ? "present" : "none")}\n" +
                $"XInput   via {(use13 ? "xinput1_3.dll" : "xinput1_4.dll")}\n" +
                $"  pad0   {Pad(0)}\n  pad1   {Pad(1)}\n  pad2   {Pad(2)}\n  pad3   {Pad(3)}\n" +
                $"USERPROFILE {Environment.GetEnvironmentVariable("USERPROFILE")}\n";
            label.Text = text;
            try { File.WriteAllText(logPath, text); } catch { }
            string extra = Environment.GetEnvironmentVariable("COOPDUMMY_LOGDIR");
            if (!string.IsNullOrEmpty(extra))
            {
                try { Directory.CreateDirectory(extra); File.WriteAllText(Path.Combine(extra, "coopdummy-" + Process.GetCurrentProcess().Id + ".log"), text); } catch { }
            }
        }
    }
}
