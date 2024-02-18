using System;
using System.Diagnostics;
using System.IO;

namespace FoxNuGet.VSTools
{
    using FoxNuGet.VSSolution;

    using Microsoft.Extensions.Logging;

    public class VisualStudioIDE
    {
        public VisualStudioIDE(string displayName, FileInfo devenvFile)
        {
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));

            DevenvFile = devenvFile ?? throw new ArgumentNullException(nameof(devenvFile));
            if (!DevenvFile.Exists)
            {
                throw new FileNotFoundException(nameof(DevenvFile));
            }
        }

        public ILogger Logger { private get; set; }

        public string DisplayName { get; }
        public FileInfo DevenvFile { get; }

        public void Build(VSProject vSProject, string configurationMode = "Release")
        {
            if (vSProject == null)
                throw new ArgumentNullException(nameof(vSProject));
            Build(vSProject.ProjectFile, configurationMode);
        }

        public void Build(VSSolution vsSolution, string configurationMode = "Release")
        {
            if (vsSolution == null)
                throw new ArgumentNullException(nameof(vsSolution));
            Build(vsSolution.SolutionFile, configurationMode);
        }

        private void Build(FileInfo solutionOrProjectFile, string configurationMode)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo(DevenvFile.FullName)
            {
                //WorkingDirectory = Project.ProjectFile.DirectoryName,
                Arguments = $"{solutionOrProjectFile} /Build {configurationMode}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process process = new Process();
            process.StartInfo = processStartInfo;
            //process.OutputDataReceived += (s, e) => { Logger?.LogInformation(8, $"devenv.exe output: {e.Data}"); };
            //process.ErrorDataReceived += (s, e) => { Logger?.LogError(8, $"devenv.exe output: {e.Data}"); };
            
            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
