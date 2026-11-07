using Nucleus.Gaming.UI;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Nucleus.Coop.Forms
{
    public partial class ShortcutsReminder : Form
    {
        private System.Windows.Forms.Timer hideTimer = new System.Windows.Forms.Timer();
        private bool IsVisible;

        private readonly string savePath = Path.Combine(Application.StartupPath, $@"gui\shortcuts");

        public ShortcutsReminder(Rectangle destBounds)
        {
            InitializeComponent();
            Cursor = Theme_Settings.Default_Cursor;

            TopMost = true;
            hideTimer.Tick += new EventHandler(HideTimerTick);
            IsVisible = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(destBounds.X + (destBounds.Width / 2 - Width / 2), destBounds.Y + (destBounds.Height / 2 - Height / 2));
            CreateHandle();
        }

        private void HideTimerTick(object Object, EventArgs EventArgs)
        {
            try
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    Hide();
                    IsVisible = false;
                    hideTimer.Stop();
                });
            }
            catch
            { }
        }

        private void ForceTopMostTimerTick(object Object, EventArgs EventArgs)
        {
            this.Invoke((MethodInvoker)delegate ()
            {
                BringToFront();
            });
        }

        public void Toggle(int closeAfterSeconds)
        {
            if (IsVisible)
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    Hide();
                    IsVisible = false;
                    hideTimer.Stop();
                });
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    gamepadImg.BackgroundImage = new Bitmap(Path.Combine(savePath, "XShortcutsReminder.jpeg"));
                    kbImg.BackgroundImage = new Bitmap(Path.Combine(savePath, "KbShortcutsReminder.jpeg"));

                    Show();
                    IsVisible = true;
                    TopMost = true;
                    hideTimer.Interval = closeAfterSeconds * 1000;
                    BringToFront();
                    hideTimer.Start();
                });
            }
        }

        public void DisposeGamepadShortcutsImg()
        {
            Invoke((MethodInvoker)delegate ()
            {
                gamepadImg.BackgroundImage?.Dispose();
            });
        }

        public void SetGamepadShortcutsImg()
        {
            Invoke((MethodInvoker)delegate ()
            {
                gamepadImg.BackgroundImage = new Bitmap(Path.Combine(savePath, "XShortcutsReminder.jpeg"));
            });
        }

        public void DisposeKbShortcutsImg()
        {
            Invoke((MethodInvoker)delegate ()
            {
                kbImg.BackgroundImage?.Dispose();
            });
        }

        public void SetKbShortcutsImg()
        {
            Invoke((MethodInvoker)delegate ()
            {
                kbImg.BackgroundImage = new Bitmap(Path.Combine(savePath, "KbShortcutsReminder.jpeg"));
            });
        }
    }
}
