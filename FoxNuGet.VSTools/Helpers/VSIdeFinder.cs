using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace FoxNuGet.VSTools
{
    public class VSIdeFinder
    {
        public static IEnumerable<VisualStudioIDE> GetExistingVS()
        {
            var vswherePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft Visual Studio\Installer\vswhere.exe");
            string registry_key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry_key))
            {
                foreach (string subkey_name in key.GetSubKeyNames())
                {
                    using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                    {
                        if (subkey.GetValue("DisplayName") != null)
                        {
                            if (subkey.GetValue("DisplayName").ToString().Contains("Visual Studio")
                                && !string.IsNullOrEmpty(subkey.GetValue("InstallLocation").ToString()))
                            {
                                string svInstallerPath = subkey?.GetValue("InstallLocation")?.ToString()?.Trim('"');
                                vswherePath = Path.Combine(svInstallerPath, "vswhere.exe");
                                break;
                            }
                        }
                    }
                }
            }

            List<VisualStudioIDE> list = new List<VisualStudioIDE>();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = vswherePath,
                    Arguments = "-all -format json",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string displayName = null;
            string productPath = null;
            FileInfo devenvFile;
            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                bool toProceed = line.Contains(nameof(displayName)) || line.Contains(nameof(productPath));
                if (toProceed)
                {
                    if (line.Contains(nameof(displayName)))
                        displayName = line.Split(new string[] { ": " }, StringSplitOptions.None)[1].Trim(',').Trim('"').Trim();
                    else
                    if (line.Contains(nameof(productPath)))
                        productPath = line.Split(new string[] { ": " }, StringSplitOptions.None)[1].Trim(',').Trim('"').Trim().Replace("\\\\", "\\").TrimEnd('\\');

                    if (displayName != null && productPath != null)
                    {
                        devenvFile = new FileInfo(productPath);
                        list.Add(new VisualStudioIDE(displayName, devenvFile));
                        displayName = null;
                        productPath = null;
                    }
                }

            }
            return list;
        }
    }
}
