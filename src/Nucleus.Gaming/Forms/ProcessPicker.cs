// Modified for PlayOut Co-Op, 2026-11-06 (GPL-3 s.5a): headless changes (dialogs/OSD through Headless.EngineUI,
// configurable install root). See README.md "Changes from Nucleus Co-op". Original: Nucleus Co-op Master/NucleusGaming/Forms/ProcessPicker.cs
using Nucleus.Gaming.Tools.GlobalWindowMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using Nucleus.Gaming.Headless;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Media;

namespace Nucleus.Gaming.Coop.Generic
{
    public partial class ProcessPicker : Form, IDynamicSized
    {
        public ProcessPicker()
        {
            InitializeComponent();
            MaximizeBox = false;
            MinimizeBox = false;

            GenericGameHandler.Instance?.AllRuntimeForms.Add(this);

            DPIManager.Register(this);
            DPIManager.AddForm(this);
            DPIManager.Update(this);         
        }

        public void UpdateSize(float scale)
        {
            if (IsDisposed)
            {
                DPIManager.Unregister(this);
                return;
            }

            float ratio = ((float)Height / (float)Width);
            Height = (int)((float)Height / (float)ratio);
            pplistBox.Font = new Font("Franklin Gothic", pplistBox.Font.Size * scale, FontStyle.Regular, GraphicsUnit.Pixel, 0);
            FormGraphicsUtil.CreateRoundedControlRegion(this, 0, 0, Width, Height, 20, 20);
        }
    }

    public static class ProcessPickerRuntime
    {
        public static Process LaunchProcessPick(PlayerInfo player)
        {
            var handlerInstance = GenericGameHandler.Instance;

            // PlayOut Co-Op: headless engines ask EngineUI instead of showing the picker form. Candidates are
            // listed in the same order the form uses (name matches first) without the already attached ones.
            if (EngineUI.Headless)
            {
                handlerInstance.Log("Launching process picker (headless)");
                string exeNameNoExt = Path.GetFileNameWithoutExtension(handlerInstance.CurrentGameInfo.ExecutableName ?? "").ToLower();
                string launcherNoExt = handlerInstance.CurrentGameInfo.LauncherExe?.Length > 0 ? Path.GetFileNameWithoutExtension(handlerInstance.CurrentGameInfo.LauncherExe).ToLower() : null;
                List<Process> matches = new List<Process>();
                List<Process> others = new List<Process>();

                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.Id == 0 || string.IsNullOrEmpty(p.MainWindowTitle) || handlerInstance.attachedIds.Contains(p.Id))
                        {
                            continue;
                        }

                        string pname = p.ProcessName.ToLower();
                        if ((exeNameNoExt.Length > 0 && pname.Contains(exeNameNoExt)) || (launcherNoExt != null && pname.Contains(launcherNoExt)))
                        {
                            matches.Add(p);
                        }
                        else
                        {
                            others.Add(p);
                        }
                    }
                    catch { }
                }

                Process picked = EngineUI.PickProcess(matches.Concat(others).ToList(), player,
                    $"Nucleus could not find the game process for {player.Nickname}. Select it.");

                if (picked == null)
                {
                    handlerInstance.Log("Process picker: no process selected");
                    return null;
                }

                handlerInstance.Log(string.Format("Obtained process {0} (pid {1}) via process picker", picked.ProcessName, picked.Id));
                handlerInstance.attached.Add(picked);
                handlerInstance.attachedIds.Add(picked.Id);
                player.ProcessID = picked.Id;

                if (player.IsKeyboardPlayer && !player.IsRawKeyboard)
                {
                    handlerInstance.keyboardProcId = picked.Id;
                }

                return picked;
            }

            ProcessPicker ppform = new ProcessPicker();
            handlerInstance.Log("Launching process picker");

            ppform.pplistBox.DoubleClick += new EventHandler(SelBtn_Click);

            foreach (Control c in ppform.Controls)
            {
                if (c.Name == "refrshBtn")
                {
                    c.Click += new EventHandler(RefrshBtn_Click);
                }
            }

            foreach (Control c in ppform.Controls)
            {
                if (c.Name == "selBtn")
                {
                    c.Click += new EventHandler(SelBtn_Click);
                }
            }

            Process[] allProc = Process.GetProcesses();

            foreach (Process p in allProc)
            {
                if (p.Id == 0 || string.IsNullOrEmpty(p.MainWindowTitle))
                {
                    continue;
                }
                if (handlerInstance.attachedIds.Contains(p.Id))
                {
                    if (p.ProcessName.ToLower().Contains(Path.GetFileNameWithoutExtension(handlerInstance.CurrentGameInfo.ExecutableName).ToLower()) || (handlerInstance.CurrentGameInfo.LauncherExe?.Length > 0 && p.ProcessName.ToLower().Contains(Path.GetFileNameWithoutExtension(handlerInstance.CurrentGameInfo.LauncherExe).ToLower())))
                    {
                        ppform.pplistBox.Items.Insert(0, p.Id + " - (DO NOT USE - Already assigned in Nucleus) " + p.ProcessName);
                    }
                    else
                    {
                        ppform.pplistBox.Items.Add(p.Id + " - (DO NOT USE - Already assigned in Nucleus) " + p.ProcessName);
                    }
                }
                else
                {
                    if (p.ProcessName.ToLower().Contains(Path.GetFileNameWithoutExtension(handlerInstance.CurrentGameInfo.ExecutableName).ToLower()) || (handlerInstance.CurrentGameInfo.LauncherExe?.Length > 0 && p.ProcessName.ToLower().Contains(Path.GetFileNameWithoutExtension(handlerInstance.CurrentGameInfo.LauncherExe).ToLower())))
                    {
                        ppform.pplistBox.Items.Insert(0, p.Id + " - " + p.ProcessName);
                    }
                    else
                    {
                        ppform.pplistBox.Items.Add(p.Id + " - " + p.ProcessName);
                    }
                }
            }

            ppform.ShowDialog();
            WindowScrape.Static.HwndInterface.MakeTopMost(ppform.Handle);

            if (ppform.pplistBox.SelectedItem != null)
            {
                Process proc = Process.GetProcessById(int.Parse(ppform.pplistBox.SelectedItem.ToString().Split(' ')[0]));
                handlerInstance.Log(string.Format("Obtained process {0} (pid {1}) via process picker", proc.ProcessName, proc.Id));
                handlerInstance.attached.Add(proc);
                handlerInstance.attachedIds.Add(proc.Id);
                player.ProcessID = proc.Id;

                if (player.IsKeyboardPlayer && !player.IsRawKeyboard)
                {
                    handlerInstance.keyboardProcId = proc.Id;
                }

                return proc;
            }

            return null;
        }

        private static void SelBtn_Click(object sender, EventArgs e)
        {
            Control control = (Control)sender;
            Form ppform = control.FindForm();

            if (control.GetType() == typeof(ListBox))
            {
                ListBox listBox = control as ListBox;
                if (listBox.SelectedItem == null)
                {
                    MessageBox.Show("No process has been selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else if (control.GetType() == typeof(Button))
            {
                foreach (Control c in ppform.Controls)
                {
                    if (c.GetType() == typeof(ListBox))
                    {
                        ListBox listBox = c as ListBox;
                        if (listBox.SelectedItem == null)
                        {
                            MessageBox.Show("No process has been selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                }
            }

            ppform.Close();
        }

        private static void RefrshBtn_Click(object sender, EventArgs e)
        {
            var handlerInstance = GenericGameHandler.Instance;

            Control control = (Button)sender;

            Form ppform = control.FindForm();
            foreach (Control l in ppform.Controls)
            {
                if (l.GetType() == typeof(ListBox))
                {
                    ListBox listBox = l as ListBox;
                    listBox.Items.Clear();

                    Process[] allProc = Process.GetProcesses();
                    foreach (Process p in allProc)
                    {
                        if (p.Id == 0 || string.IsNullOrEmpty(p.MainWindowTitle))
                        {
                            continue;
                        }
                        if (handlerInstance.attachedIds.Contains(p.Id))
                        {
                            if (p.ProcessName.ToLower().Contains(Path.GetFileNameWithoutExtension(handlerInstance.CurrentGameInfo.ExecutableName).ToLower()) || (handlerInstance.CurrentGameInfo.LauncherExe?.Length > 0 && p.ProcessName.ToLower().Contains(Path.GetFileNameWithoutExtension(handlerInstance.CurrentGameInfo.LauncherExe).ToLower())))
                            {
                                listBox.Items.Insert(0, p.Id + " - (DO NOT USE - Already assigned in Nucleus)" + p.ProcessName);
                            }
                            else
                            {
                                listBox.Items.Add(p.Id + " - (DO NOT USE - Already assigned in Nucleus)" + p.ProcessName);
                            }
                        }
                        else
                        {
                            if (p.ProcessName.ToLower().Contains(Path.GetFileNameWithoutExtension(handlerInstance.CurrentGameInfo.ExecutableName).ToLower()) || (handlerInstance.CurrentGameInfo.LauncherExe?.Length > 0 && p.ProcessName.ToLower().Contains(Path.GetFileNameWithoutExtension(handlerInstance.CurrentGameInfo.LauncherExe).ToLower())))
                            {
                                listBox.Items.Insert(0, p.Id + " - " + p.ProcessName);
                            }
                            else
                            {
                                listBox.Items.Add(p.Id + " - " + p.ProcessName);
                            }
                        }
                    }
                }
            }
        }
    }
}
