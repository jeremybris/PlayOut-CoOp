// PlayOut Co-Op (GPL-3): "--prompts native". The engine requests that must wait for the player (Nucleus's
// "Press OK when..." prompts, Yes/No questions, handler text prompts, the launcher-exe file picker) are shown as real
// top-most Windows dialogs, and the engine waits for the answer, as it did with Nucleus's own dialogs.
using Nucleus.Gaming.Headless;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PlayOutCoop
{
    internal static class NativePrompts
    {
        /// <summary>True with --prompts native.</summary>
        public static bool Enabled;

        private static volatile bool cancelled;
        private static readonly object Gate = new object();          // one dialog at a time
        private static readonly object ThreadsLock = new object();
        private static readonly List<uint> DialogThreads = new List<uint>();
        private static bool stylesEnabled;

        /// <summary>The requests that wait for the player. Informational messages are not among them.</summary>
        public static bool IsWaiting(EngineUiRequest req)
        {
            switch (req.Kind)
            {
                case EngineUiKind.Prompt:
                case EngineUiKind.CustomPrompt:
                case EngineUiKind.SelectFile:
                    return true;
                case EngineUiKind.MessageBox:
                    return req.Buttons != MessageBoxButtons.OK;
                default:
                    return false;
            }
        }

        /// <summary>"ok", "yesno", "text" or "file" (the contract's prompt kinds).</summary>
        public static string KindOf(EngineUiRequest req)
        {
            switch (req.Kind)
            {
                case EngineUiKind.CustomPrompt: return "text";
                case EngineUiKind.SelectFile: return "file";
                case EngineUiKind.MessageBox: return "yesno";
                default: return "ok";
            }
        }

        public static string TitleOf(EngineUiRequest req)
        {
            return string.IsNullOrWhiteSpace(req.Title) || req.Title == "Nucleus" ? "PlayOut Co-Op" : req.Title;
        }

        public static bool Cancelled => cancelled;

        /// <summary>
        /// Closes any open prompt (as Cancel) and answers every later one with its default: the session is ending.
        /// </summary>
        public static void CancelAll()
        {
            cancelled = true;
            uint[] threads;
            lock (ThreadsLock)
            {
                threads = DialogThreads.ToArray();
            }

            foreach (uint tid in threads)
            {
                EnumThreadWindows(tid, (hwnd, _) =>
                {
                    PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }, IntPtr.Zero);
            }
        }

        /// <summary>Shows the dialog and blocks until it is answered. Null = take the engine's default.</summary>
        public static EngineUiResponse Show(EngineUiRequest req)
        {
            lock (Gate)
            {
                if (cancelled)
                {
                    return null;
                }

                EngineUiResponse result = null;
                Exception error = null;
                Thread t = new Thread(() =>
                {
                    uint tid = GetCurrentThreadId();
                    lock (ThreadsLock)
                    {
                        DialogThreads.Add(tid);
                    }

                    try
                    {
                        if (!stylesEnabled)
                        {
                            stylesEnabled = true;
                            try { Application.EnableVisualStyles(); } catch { }
                        }

                        result = ShowOnThisThread(req);
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        lock (ThreadsLock)
                        {
                            DialogThreads.Remove(tid);
                        }
                    }
                })
                {
                    IsBackground = true,
                    Name = "prompt",
                };
                t.SetApartmentState(ApartmentState.STA);
                t.Start();

                // A stop that arrived before the dialog window existed: close it as soon as it does.
                while (!t.Join(250))
                {
                    if (cancelled)
                    {
                        CancelAll();
                    }
                }

                if (error != null)
                {
                    Output.Log("Native prompt failed, using the default answer: " + error);
                    return null;
                }

                return cancelled ? CancelledAnswer(req) : result;
            }
        }

        private static EngineUiResponse CancelledAnswer(EngineUiRequest req)
        {
            // The session is ending: continue (OK) for pauses, no file, no text, the default button for questions.
            switch (req.Kind)
            {
                case EngineUiKind.SelectFile:
                case EngineUiKind.CustomPrompt:
                    return EngineUiResponse.Of(DialogResult.Cancel);
                case EngineUiKind.MessageBox:
                    return EngineUiResponse.Of(req.DefaultResult);
                default:
                    return EngineUiResponse.Of(DialogResult.OK);
            }
        }

        private static EngineUiResponse ShowOnThisThread(EngineUiRequest req)
        {
            string title = TitleOf(req);
            switch (req.Kind)
            {
                case EngineUiKind.SelectFile:
                    return PickFile(title, req);

                case EngineUiKind.CustomPrompt:
                    using (PromptForm f = new PromptForm(title, req.Message, new[] { ("OK", DialogResult.OK), ("Cancel", DialogResult.Cancel) }, 0, true, req.DefaultValue))
                    {
                        DialogResult r = f.ShowDialog();
                        return r == DialogResult.OK
                            ? new EngineUiResponse { Result = DialogResult.OK, Value = f.InputText }
                            : EngineUiResponse.Of(DialogResult.Cancel);
                    }

                case EngineUiKind.MessageBox:
                {
                    var buttons = ButtonsOf(req.Buttons);
                    int def = req.DefaultButton == MessageBoxDefaultButton.Button3 ? 2 : req.DefaultButton == MessageBoxDefaultButton.Button2 ? 1 : 0;
                    using (PromptForm f = new PromptForm(title, req.Message, buttons, Math.Min(def, buttons.Length - 1), false, null))
                    {
                        DialogResult r = f.ShowDialog();
                        bool hasCancel = Array.Exists(buttons, b => b.Item2 == DialogResult.Cancel);
                        if (r == DialogResult.Cancel && !hasCancel)
                        {
                            // Closed with the window's X on a Yes/No question: the default button, as Enter would give.
                            r = req.DefaultResult;
                        }

                        return EngineUiResponse.Of(r);
                    }
                }

                default: // Prompt: "Press OK when ..."
                    using (PromptForm f = new PromptForm(title, req.Message, new[] { ("OK", DialogResult.OK) }, 0, false, null))
                    {
                        f.ShowDialog();
                        return EngineUiResponse.Of(DialogResult.OK);
                    }
            }
        }

        private static (string, DialogResult)[] ButtonsOf(MessageBoxButtons b)
        {
            switch (b)
            {
                case MessageBoxButtons.OKCancel: return new[] { ("OK", DialogResult.OK), ("Cancel", DialogResult.Cancel) };
                case MessageBoxButtons.YesNoCancel: return new[] { ("Yes", DialogResult.Yes), ("No", DialogResult.No), ("Cancel", DialogResult.Cancel) };
                case MessageBoxButtons.RetryCancel: return new[] { ("Retry", DialogResult.Retry), ("Cancel", DialogResult.Cancel) };
                case MessageBoxButtons.AbortRetryIgnore: return new[] { ("Abort", DialogResult.Abort), ("Retry", DialogResult.Retry), ("Ignore", DialogResult.Ignore) };
                case MessageBoxButtons.YesNo: return new[] { ("Yes", DialogResult.Yes), ("No", DialogResult.No) };
                default: return new[] { ("OK", DialogResult.OK) };
            }
        }

        private static EngineUiResponse PickFile(string title, EngineUiRequest req)
        {
            // A common dialog is only top-most when its owner is: an invisible top-most window of our own owns it.
            using (Form owner = new Form
            {
                TopMost = true,
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterScreen,
                Size = new Size(1, 1),
                Opacity = 0,
                Text = title,
            })
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                owner.Show();
                ofd.Title = string.IsNullOrWhiteSpace(req.Message) ? title : req.Message;
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.Filter = string.IsNullOrWhiteSpace(req.Detail)
                    ? "Programs (*.exe)|*.exe|All files (*.*)|*.*"
                    : $"{req.Detail}|{req.Detail}|Programs (*.exe)|*.exe|All files (*.*)|*.*";
                SetForegroundWindow(owner.Handle);
                DialogResult r = ofd.ShowDialog(owner);
                return r == DialogResult.OK && !string.IsNullOrEmpty(ofd.FileName)
                    ? new EngineUiResponse { Result = DialogResult.OK, Value = ofd.FileName }
                    : EngineUiResponse.Of(DialogResult.Cancel);
            }
        }

        /// <summary>A plain top-most dialog with no owner: message, optional text box, buttons. Enter = default button.</summary>
        private sealed class PromptForm : Form
        {
            private readonly TextBox input;
            public string InputText => input?.Text;

            public PromptForm(string title, string message, (string, DialogResult)[] buttons, int defaultIndex, bool withInput, string inputDefault)
            {
                Text = title;
                Font = SystemFonts.MessageBoxFont;
                AutoScaleMode = AutoScaleMode.None;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowIcon = false;
                ShowInTaskbar = true;
                TopMost = true;
                StartPosition = FormStartPosition.CenterScreen;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;

                float scale;
                using (Graphics g = CreateGraphics())
                {
                    scale = g.DpiX / 96f;
                }
                int S(int v) => (int)Math.Round(v * scale);

                Padding = new Padding(S(16), S(16), S(16), S(12));

                TableLayoutPanel layout = new TableLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = 1,
                    Dock = DockStyle.Fill,
                };

                Label label = new Label
                {
                    Text = message ?? "",
                    AutoSize = true,
                    MaximumSize = new Size(S(520), 0),
                    MinimumSize = new Size(S(320), 0),
                    Margin = new Padding(0, 0, 0, S(12)),
                    UseMnemonic = false,
                };
                layout.Controls.Add(label);

                if (withInput)
                {
                    input = new TextBox
                    {
                        Text = inputDefault ?? "",
                        Width = S(520),
                        Margin = new Padding(0, 0, 0, S(12)),
                    };
                    layout.Controls.Add(input);
                }

                FlowLayoutPanel row = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.RightToLeft,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0),
                    WrapContents = false,
                };

                Button[] made = new Button[buttons.Length];
                for (int i = buttons.Length - 1; i >= 0; i--)
                {
                    Button b = new Button
                    {
                        Text = buttons[i].Item1,
                        DialogResult = buttons[i].Item2,
                        AutoSize = true,
                        MinimumSize = new Size(S(88), S(28)),
                        Margin = new Padding(S(8), 0, 0, 0),
                        UseVisualStyleBackColor = true,
                    };
                    made[i] = b;
                    row.Controls.Add(b);
                    if (buttons[i].Item2 == DialogResult.Cancel)
                    {
                        CancelButton = b;
                    }
                }

                layout.Controls.Add(row);
                Controls.Add(layout);

                AcceptButton = made[Math.Max(0, Math.Min(defaultIndex, made.Length - 1))];
                ActiveControl = withInput ? (Control)input : made[Math.Max(0, Math.Min(defaultIndex, made.Length - 1))];
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                // Not owned by any window; top-most, and brought forward as far as Windows allows.
                Activate();
                SetForegroundWindow(Handle);
                FlashWindow(Handle, true);
            }
        }

        private const uint WM_CLOSE = 0x0010;
        private delegate bool EnumThreadWndProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWndProc lpfn, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool FlashWindow(IntPtr hWnd, bool invert);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    }
}
