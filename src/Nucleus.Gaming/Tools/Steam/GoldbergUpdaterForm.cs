using Ionic.Zip;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

namespace Nucleus.Gaming.Tools.Steam
{
    public partial class GoldbergUpdaterForm : Form
    {
        public static string destinationPath = Path.Combine(Globals.NucleusInstallRoot, "utils\\GoldbergEmu");
        private ZipFile zip = null;

        public GoldbergUpdaterForm()
        {
            InitializeComponent();
            label.Location = new Point(Width / 2 - label.Width / 2, panel.Height / 2 - label.Height / 2);
            button.Location = new Point(panel.Width / 2 - button.Width / 2, panel.Height / 2 - button.Height / 2);
            button.BackColor = Color.FromArgb(200, 0, 0, 0);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DeleteTemp();
            DownloadReleaseZip();
        }

        private void DownloadReleaseZip()
        {
            TopMost = true;

            if (!Directory.Exists(Path.Combine(destinationPath, @"Temp")))
            {
                Directory.CreateDirectory(Path.Combine(destinationPath, @"Temp"));
            }

            button.Visible = false;
            label.Text = "Downloading do not exit...";

            SuspendLayout();
            panel.BackColor = Color.FromArgb(180, 0, 0, 0);
            label.Location = new Point(Width / 2 - label.Width / 2, panel.Height / 2 - label.Height / 2);
            ResumeLayout();

            try
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                    ServicePointManager.DefaultConnectionLimit = 9999;

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create($@"https://gitlab.com/Mr_Goldberg/goldberg_emulator/-/jobs/artifacts/master/download?job=deploy_all");
                    request.UserAgent = "request";

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream stream = response.GetResponseStream())
                    {
                        using (var fileStream = new FileStream(Path.Combine(destinationPath, @"Temp\gb.zip"), FileMode.Create, FileAccess.Write))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }

                    bool isValidZip = ZipFile.CheckZip(Path.Combine(destinationPath, @"Temp\gb.zip"));

                    if (isValidZip)
                    {
                        zip = new ZipFile(Path.Combine(destinationPath, @"Temp\gb.zip"));
                        zip.ExtractExistingFile = ExtractExistingFileAction.OverwriteSilently;
                        zip.ExtractAll(destinationPath);
                        zip.Dispose();
                    }
                    else
                    {
                        MessageBox.Show("Zip file is missing or corrupted.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    Invoke(new Action(delegate
                    {
                        label.Text = "Update Completed!";
                        label.Location = new Point(Width / 2 - label.Width / 2, panel.Height / 2 - label.Height / 2);
                        DeleteTemp();
                    }));

                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void DeleteTemp()
        {
            try
            {
                Directory.Delete(Path.Combine(destinationPath, @"Temp"), true);
            }
            catch
            { }
        }

        private void GoldbergUpdaterForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            DeleteTemp();
        }

    }
}
