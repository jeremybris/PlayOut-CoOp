using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Nucleus.Gaming.Util
{
    public class RegistryUtil
    {
        public static void ExportRegistry(string strKey, string filepath)
        {
            try
            {
                using (Process proc = new Process())
                {
                    proc.StartInfo.FileName = "reg.exe";
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.StartInfo.RedirectStandardError = true;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.StartInfo.Arguments = "export \"" + strKey + "\" \"" + filepath + "\" /y";
                    proc.Start();
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                }
            }
            catch (Exception)
            {
                // handle exception
            }
        }

        public static void RestoreUserEnvironmentRegistryPath()
        {
            string[] environmentRegFile = Directory.GetFiles(Path.Combine(Globals.NucleusInstallRoot, "utils\\backup"), "*.reg", SearchOption.AllDirectories);

            if (environmentRegFile.Length > 0)
            {
                LogManager.Log("Restoring default user environment path");

                foreach (string environmentRegFilePath in environmentRegFile)
                {
                    if (environmentRegFilePath.Contains("User Shell Folders"))
                    {
                        Process proc = new Process();

                        try
                        {
                            proc.StartInfo.FileName = "reg.exe";
                            proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                            proc.StartInfo.CreateNoWindow = true;
                            proc.StartInfo.UseShellExecute = false;

                            string command = "import \"" + environmentRegFilePath + "\"";
                            proc.StartInfo.Arguments = command;
                            proc.Start();

                            proc.WaitForExit();
                            LogManager.Log($"Imported {Path.GetFileName(environmentRegFilePath)}");
                        }
                        catch (Exception)
                        {
                            proc.Dispose();
                        }
                    }
                }
            }
        }

        public static void RestoreRegistry(string step)
        {
            string[] regFiles = Directory.GetFiles(Path.Combine(Globals.NucleusInstallRoot, "utils\\backup"), "*.reg", SearchOption.AllDirectories);
            if (regFiles.Length > 0)
            {
                LogManager.Log("Restoring backed up registry files " + step);
                foreach (string regFilePath in regFiles)
                {
                    Process proc = new Process();

                    try
                    {
                        proc.StartInfo.FileName = "reg.exe";
                        proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        proc.StartInfo.CreateNoWindow = true;
                        proc.StartInfo.UseShellExecute = false;

                        string command = "import \"" + regFilePath + "\"";
                        proc.StartInfo.Arguments = command;
                        proc.Start();

                        proc.WaitForExit();
                        LogManager.Log($"Imported {Path.GetFileName(regFilePath)}");
                    }
                    catch (Exception)
                    {
                        proc.Dispose();
                    }

                    if (!regFilePath.Contains("User Shell Folders"))
                    {
                        File.Delete(regFilePath);
                    }
                }
            }

        }

    }
}
