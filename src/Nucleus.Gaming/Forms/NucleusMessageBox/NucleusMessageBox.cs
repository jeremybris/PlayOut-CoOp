
using System;
using System.Windows.Forms;
using System.Runtime.CompilerServices;
using Nucleus.Gaming.Headless;

namespace Nucleus.Gaming.Forms
{
    public static class NucleusMessageBox
    {
        public static CustomMessageBox MessageBox;

        public static void Show(string title, string message, bool formating,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            // PlayOut Co-Op: headless engines never show this form; the message goes to EngineUI instead.
            if (EngineUI.Headless)
            {
                EngineUI.NucleusMessageBox(title, message, file, member, line);
                return;
            }

            MessageBox?.Invoke((MethodInvoker)delegate ()
            {
                MessageBox.Close();
            });

            MessageBox = new CustomMessageBox(title, message, formating);
            MessageBox.FormClosed += MessageBox_Dispose;
            
            MessageBox.ShowDialog();      
            MessageBox?.BringToFront();
        }

        private static void MessageBox_Dispose(object sender, EventArgs e)
        {
            MessageBox?.Invoke((MethodInvoker)delegate ()
            {
                MessageBox.Dispose();
            });

            MessageBox = null;
        }
    }
}
