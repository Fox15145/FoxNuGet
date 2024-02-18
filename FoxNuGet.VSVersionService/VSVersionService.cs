using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


using Microsoft.Extensions.Logging;

using NuGet.Versioning;

namespace FoxNuGet.VSVersionService
{
    using FoxNuGet.VSGit;
    using FoxNuGet.VSNuGet;
    using FoxNuGet.VSSolution;
    using FoxNuGet.VSTools;

    public sealed class VSVersionService
    {
        private IEnumerable<VSProject> _modifiedProjects;

        public VSVersionService(DirectoryInfo workingDirectory)
        {
            if (workingDirectory is null)
            {
                throw new ArgumentNullException(nameof(workingDirectory));
            }
            WorkingDirectory = workingDirectory;
        }

        public DirectoryInfo WorkingDirectory { get; }
        public VisualStudioIDE VsVisualStudio { get; private set; }
        public bool HasSourceCodeVersionTool { get; private set; }
        public IEnumerable<VSSolution> VsSolutions { get; private set; }
        public List<VSNuGet> VsNuGets { get; private set; }
        public DirectoryInfo OutputDirectory { get; private set; }
        public ILogger Logger { private get; set; }

        public void Refresh()
        {
            Logger?.LogInformation(0, $"FoxNuget work in \"{WorkingDirectory.FullName}\" :");

            GetSolutionsInformations();

            GetIDEInformations();
        }

        private void GetSolutionsInformations()
        {
            int logLevel = 1;
            VsSolutions = GetAllSolution(WorkingDirectory);
            Logger?.LogInformation(logLevel, $"Solution(s) *.sln count : {VsSolutions.Count()}");

            VsNuGets = new List<VSNuGet>();

            int solutionIndex = 1;
            foreach (VSSolution vsSolution in VsSolutions)
            {
                Logger?.LogInformation(logLevel, $"*** Solution N°{solutionIndex++}");
                Logger?.LogInformation(logLevel, $"  Name : \"{vsSolution.SolutionFile.Name}\"");
                Logger?.LogInformation(logLevel, $"  Path : \"{vsSolution.SolutionFile.Directory.FullName}\"");
                Logger?.LogInformation(logLevel, $"  Project(s) *.csproj count : {vsSolution.Projects?.Count() ?? 0}");

                VSGit vsGit = null;
                try
                {
                    vsGit = GetGitInformations(vsSolution);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(logLevel, ex, "No local git repository found.");
                }
                HasSourceCodeVersionTool = vsGit != null;

                if (!HasSourceCodeVersionTool)
                    continue;

                GetGitInformations(vsSolution, vsGit);
                _ = GetNuSpecInformations(vsSolution);
                GetNuGetInformations(vsGit);
                Logger?.LogInformation(logLevel, "");
            }
        }

        private void GetIDEInformations()
        {
            IEnumerable<VisualStudioIDE> IDEs = VSIdeFinder.GetExistingVS();
            Logger?.LogInformation(new EventId(0), $"IDE Visual Studio found : {IDEs?.Count() ?? 0}");

            VsVisualStudio = IDEs.OrderByDescending(i => i.DisplayName).FirstOrDefault();
            VsVisualStudio.Logger = Logger;
            Logger?.LogInformation(new EventId(0), $"IDE Visual Studio selected : {VsVisualStudio?.DisplayName ?? "None"}");
        }

        private void GetNuGetInformations(VSGit vsGit)
        {
            int nugetIndex = 1;
            foreach (VSNuGet vsNuGet in VsNuGets)
            {
                Logger?.LogInformation(4, $"  *** NuGet available N°{nugetIndex++}");
                IEnumerable<VSProject> impactedProjects = GetModifiedProjects(vsNuGet, vsGit);
                Logger?.LogInformation(4, $"    Name : {vsNuGet.Nuspec.NuSpecFile.Name} (v{vsNuGet.Project.Version})");
                Logger?.LogInformation(4, $"    UniqueID : {vsNuGet.Nuspec.UniqueId}");
                Logger?.LogInformation(4, $"      Project(s) impacted by versioning : {impactedProjects?.Count() ?? 0}");
                foreach (VSProject project in impactedProjects)
                {
                    Logger?.LogInformation(4, $"        * \"{project.ProjectFile.Name}\"");
                }
                Logger?.LogInformation(4, $"      -> Version DEBUG to generate : v{vsNuGet.GetVersionToGenerate("DEBUG")}");
                Logger?.LogInformation(4, $"      -> Version RELEASE to generate : v{vsNuGet.GetVersionToGenerate("RELEASE")}");
                CheckMissingProjectReferenceInSolution(impactedProjects);
                CheckMissingFileInNuSpec(vsNuGet);
                CheckMissingProjectFileInNuSpec(vsNuGet);
                Logger?.LogInformation(4, $"Nuspec is ready: {vsNuGet.Nuspec.IsReady}");
            }
        }

        private void CheckMissingProjectFileInNuSpec(VSNuGet vsNuGet)
        {
            IsAssemblyExistsInNuSpec(vsNuGet, vsNuGet.Project.AssemblyName);

            foreach (string assemblyName in vsNuGet.Project.References.Where(r => r is VSProject).Select(p => (p as VSProject).AssemblyName))
            {
                IsAssemblyExistsInNuSpec(vsNuGet, assemblyName);

            }

            //TODO : To move in specific method to found subproject used into the nuspec file.
            //foreach (VSNugetFile vsNugetFile in VsNuGets.Last().Nuspec.GetFiles().Where(n => n != null))
            //{
            //    VSProject impactedSubProject = vsSolution.Projects.FirstOrDefault(p => p.Name.Contains(Path.GetFileNameWithoutExtension(vsNugetFile.Source)));
            //    if (impactedProject is null)
            //        continue;
            //}
        }

        private void IsAssemblyExistsInNuSpec(VSNuGet vsNuGet, string assemblyName)
        {
            if (!vsNuGet.Nuspec.GetFiles().Any(n => Path.GetFileName(n.Source).Equals(assemblyName, StringComparison.OrdinalIgnoreCase)))
                Logger?.LogWarning(4, $"    \"{assemblyName}\" missing in nuSpec.");
        }

        private void CheckMissingFileInNuSpec(VSNuGet vsNuget)
        {
            foreach (FileInfo file in vsNuget.Nuspec.GetFiles()
                .Where(f => Path.GetExtension(f.Source).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileInfo(Path.Combine(vsNuget.Nuspec.NuSpecFile.DirectoryName, f.Source))))
            {
                if (vsNuget.Project.AssemblyName.Equals(file.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (vsNuget.Project.References.Any(p => p is VSProject project && project.AssemblyName.Equals(file.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (file.Exists)
                    continue;

                Logger?.LogWarning(4, $"NuSpec resource file missing (may be not already compile): {file.FullName}");
            }
        }

        private void CheckMissingProjectReferenceInSolution(IEnumerable<VSProject> impactedProjects)
        {
            foreach (VSProject project in impactedProjects)
            {
                var warnings = project.ShowWarning();
                if (!warnings.Any())
                    continue;

                Logger?.LogWarning(4, $"        * \"{project.ProjectFile.Name}\" warning:");
                warnings.ToList().ForEach(w => Logger?.LogWarning(4, $"          -> {w}."));
            }
        }

        /// <summary>
        /// Finds all *.nuspec files handle by a specific solution (*.sln) directory 
        /// and return an impacted <see cref="VSProject"/> list.
        /// </summary>
        /// <param name="vsSolution">Solution handle by the research.</param>
        private IEnumerable<VSProject> GetNuSpecInformations(VSSolution vsSolution)
        {
            Logger?.LogInformation(new EventId(3), $"OutputDirectory : \"{OutputDirectory}\"");

            IEnumerable<FileInfo> nuspecFilesFound = Directory.GetFiles(vsSolution.SolutionFile.Directory.FullName, "*.nuspec", SearchOption.AllDirectories).Select(f => new FileInfo(f));
            IEnumerable<FileInfo> nuspecFiles = nuspecFilesFound.Where(f => !f.FullName.Contains(@"\obj\"));

            Logger?.LogInformation(new EventId(3), $"  Package(s) *.NuSpec count : {nuspecFiles?.Count() ?? 0}");
            var impactedProjects = new HashSet<VSProject>();

            if (nuspecFiles != null)
            {
                int maxLength = nuspecFiles.Select(n => n.Name.Length).OrderByDescending(l => l).FirstOrDefault();

                foreach (FileInfo nuspecFile in nuspecFiles)
                {
                    Logger?.LogInformation(new EventId(3), $"    * {nuspecFile.Name.PadRight(maxLength + 2)} :  {VSNuSpec.GenerateUniqueId(nuspecFile)}");

                    VSProject nuSpecProject = vsSolution.Projects.FirstOrDefault(p => p.Name.Equals(Path.GetFileNameWithoutExtension(nuspecFile.Name)));
                    if (nuSpecProject is null)
                        continue;

                    VSNuGet vSNuGet = new VSNuGet(nuSpecProject, OutputDirectory, Logger);

                    if (_modifiedProjects.Any(p => p.ProjectFile.Equals(nuSpecProject.ProjectFile)))
                    {
                        impactedProjects.Add(nuSpecProject);
                        VsNuGets.Add(vSNuGet);
                    }

                    foreach (string assembly in vSNuGet.Nuspec.GetFiles().Select(a => a.Source))
                    {
                        if (_modifiedProjects.Any(p => assembly.Contains(p.AssemblyName)))
                        {
                            impactedProjects.Add(nuSpecProject);

                            if (!VsNuGets.Contains(vSNuGet))
                                VsNuGets.Add(vSNuGet);
                        }
                    }
                }
            }
            Logger?.LogInformation(new EventId(3), $"  Package(s) handle with *.csproj : {VsNuGets?.Count() ?? 0}");
            return impactedProjects;
        }

        private void GetGitInformations(VSSolution vsSolution, VSGit vsGit)
        {
            if (vsGit is null)
            {
                Logger?.LogInformation(2, $"  No Git LocalRepositoryPath found.");

                OutputDirectory = new DirectoryInfo(Path.Combine(vsSolution.SolutionFile.Directory.Parent.FullName, "Packages"));
                return;
            }

            OutputDirectory = new DirectoryInfo(Path.Combine(vsGit.LocalRepositoryPath.Parent.FullName, "Packages"));
            Logger?.LogInformation(2, $"  Package(s) *.NuGet output directory :");
            Logger?.LogInformation(2, $"    * \"{OutputDirectory.FullName}\"");
            Logger?.LogInformation(2, "");

            Logger?.LogInformation(2, $"  Git LocalRepositoryPath : \"{vsGit.LocalRepositoryPath}\"");
            Logger?.LogInformation(2, $"  Git MasterBranch : {vsGit.MasterBranch}");
            Logger?.LogInformation(2, $"  Git CurrentBranch : {vsGit.CurrentBranch}");
            Logger?.LogInformation(2, $"  Git CommonAncestorCommit : {vsGit.CommonAncestorCommit}");
            Logger?.LogInformation(2, $"  Git commit(s) count : {vsGit.CommitLog?.Count() ?? 0}");
            Logger?.LogInformation(2, $"  Git Modified files count : {vsGit.ModifiedFiles?.Count() ?? 0}");
            _modifiedProjects = GetModifiedProjects(vsSolution, vsGit);
            Logger?.LogInformation(2, $"  Project(s) impacted by modifications : {_modifiedProjects?.Count() ?? 0}");
            if (_modifiedProjects != null)
            {
                foreach (VSProject project in _modifiedProjects)
                {
                    Logger?.LogInformation(2, $"    * \"{project.ProjectFile.Name}\"");
                }
            }
            Logger?.LogInformation(2, "");
        }

        public void GenerateNuGet(VSNuGet vsNuGet, VisualStudioIDE vsVisualStudio, string configurationMode = "DEBUG")
        {
            int logLevel = 6;
            Logger?.LogInformation(logLevel, $"Prepare to generate \"{vsNuGet.Nuspec.NuSpecFile.Name}\" NuGet...");

            if (vsNuGet is null)
            {
                throw new ArgumentNullException(nameof(vsNuGet));
            }

            if (vsVisualStudio is null)
            {
                throw new ArgumentNullException(nameof(vsVisualStudio));
            }

            if (string.IsNullOrEmpty(configurationMode))
            {
                throw new ArgumentException($"« {nameof(configurationMode)} » ne peut pas être vide ou avoir la valeur Null.", nameof(configurationMode));
            }


            List<string> releaseNotes = new List<string>();
            VSGit vsGit = null;
            try
            {
                vsGit = GetGitInformations(vsNuGet.Project.OwnerSolution);
                releaseNotes.Add($"{DateTime.Now}");
                releaseNotes.AddRange(vsGit.Messages.Select(message => $"   • {message}").ToList());
            }
            catch (Exception ex)
            {
                Logger?.LogInformation(logLevel, ex, "No Release notes found.");
            }

            NuGetVersion versionToGenerate = vsNuGet.Prepare(configurationMode, _modifiedProjects, releaseNotes);
            if (versionToGenerate is null)
            {
                Logger?.LogInformation(logLevel, "Fail to prepare NuGet generation.");
                return;
            }
            Logger?.LogInformation(logLevel, $"Project(s) building with \"{vsVisualStudio.DisplayName}\" in \"{configurationMode}\" mode...");
            vsVisualStudio.Build(vsNuGet.Project, configurationMode);

            Logger?.LogInformation(logLevel, $"\"{Path.GetFileNameWithoutExtension(vsNuGet.Nuspec.NuSpecFile.Name)}.nupkg\" generation in progress ...");
            vsNuGet.Generate(versionToGenerate);
        }

        public void GenerateNuGet(VSNuGet vsNuGet, string configurationMode = "DEBUG")
        {
            GenerateNuGet(vsNuGet, VsVisualStudio, configurationMode);
        }

        public VSNuGet GenerateNuSpec(VSProject vsProject)
        {
            return new VSNuGet(vsProject, OutputDirectory);
        }

        public IEnumerable<VSSolution> GetAllSolution(DirectoryInfo targetPath)
        {
            if (targetPath is null)
            {
                throw new ArgumentNullException(nameof(targetPath));
            }

            if (!targetPath.Exists)
                throw new DirectoryNotFoundException(targetPath.FullName);

            return Directory.GetFiles(targetPath.FullName, "*.sln", SearchOption.AllDirectories).Select(solutionPath => new VSSolution(new FileInfo(solutionPath)));
        }

        public VSGit GetGitInformations(VSSolution solution)
        {
            if (solution is null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            return new VSGit(solution.SolutionFile.Directory);
        }

        public IEnumerable<VSProject> GetModifiedProjects(VSSolution vsSolution, VSGit vsGit)
        {
            if (vsSolution is null)
            {
                throw new ArgumentNullException(nameof(vsSolution));
            }

            if (vsGit is null)
            {
                return vsSolution.Projects;
                //throw new ArgumentNullException(nameof(vsGit));
            }

            List<VSProject> impactedProjects = new List<VSProject>();

            if (vsGit.ModifiedFiles is null)
                return impactedProjects;

            foreach (VSProject project in vsSolution.Projects)
            {
                if (!vsGit.ModifiedFiles.Any(file => file.FullName.Contains(project.ProjectFile.Directory.FullName)))
                    continue;

                impactedProjects.Add(project);
            }

            return impactedProjects;
        }

        public IEnumerable<VSProject> GetModifiedProjects(VSNuGet vsNuget, VSGit vsGit)
        {
            if (vsNuget is null)
            {
                throw new ArgumentNullException(nameof(vsNuget));
            }

            List<VSProject> impactedProjects = new List<VSProject>()
            {
                vsNuget.Project
            };

            if (vsGit is null)
            {
                return impactedProjects;
            }

            foreach (VSProject project in vsNuget.Project.References.Where(r => r is VSProject))
            {
                if (vsGit.ModifiedFiles?.Any(file => file.FullName.Contains(project.ProjectFile.Directory.FullName)) == true)
                    impactedProjects.Add(project);
            }

            return impactedProjects;
        }

        public VSNuGet GetNuGet(string nuSpecUniqueId)
        {
            return VsNuGets.FirstOrDefault(nuGet => nuGet.Nuspec.UniqueId.Equals(nuSpecUniqueId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
