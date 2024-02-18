using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FoxNuGet.VSSolution
{
    public sealed class VSSolution
    {
        private readonly List<VSProject> _projects = new List<VSProject>();

        private readonly FileSystemWatcher _watcher = new FileSystemWatcher();
        
        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            OnSolutionFileChanged?.Invoke(this);

        }

        public VSSolution(FileInfo solutionFile)
        {
            SolutionFile = solutionFile ?? throw new ArgumentNullException(nameof(solutionFile));
            
            if (!solutionFile.FullName.ToLower().EndsWith(".sln"))
                throw new InvalidDataException($"{nameof(solutionFile)} argument must be be a visual studio solution file name (.sln) : \"{solutionFile}\".");
            
            if (!SolutionFile.Exists)
                throw new FileNotFoundException($"{nameof(solutionFile)} : \"{solutionFile}\".");
            
            IsProjectsLoaded = false;
            LoadProjectsAsync().GetAwaiter();
            InitializeFileWatcher();
        }

        public event Action OnProjectsLoaded;

        public FileInfo SolutionFile { get; }

        public bool IsProjectsLoaded { get; private set; }

        public IEnumerable<VSProject> Projects => _projects;

        public event Action<VSSolution> OnSolutionFileChanged;

        public async Task LoadProjectsAsync()
        {
            bool isLoadingProject = false;
            foreach (string line in File.ReadAllLines(SolutionFile.FullName))
            {
                if (line.StartsWith("Project(\"") && line.Contains(".csproj"))
                {
                    isLoadingProject = true;
                    string[] projectElements = line.Split(new string[] { ", "}, StringSplitOptions.None);
                    string[] projectDetails = projectElements[0].Split('=');
                    string projectUniqueId = projectDetails[0].Replace("Project(\"", string.Empty).Replace("\")", string.Empty).Trim();
                    string projectName = projectDetails[1].Replace("\"", string.Empty).Trim();
                    string projectFilename = Path.Combine(SolutionFile.DirectoryName, projectElements[1].Replace("\"", string.Empty).Trim());
                    FileInfo projectFile = null;
                    if (File.Exists(projectFilename))
                        projectFile = new FileInfo(projectFilename);

                    VSProject vsProject = new VSProject(projectUniqueId, projectName, projectFile, this);
                    vsProject.OnStatusChanged += VsProject_OnStatusChanged;
                    _projects.Add(vsProject);
                }
                else
                if (isLoadingProject && line.StartsWith("EndProject"))
                {
                    isLoadingProject = false;
                }
                else
                if (isLoadingProject)
                {
                    //TODO : Load specifics project informations.
                }
                else
                {
                    //TODO : Load specifics solution configuration informations.
                }
            }

            VsProject_OnStatusChanged(null);
        }

        private void VsProject_OnStatusChanged(VSProject obj)
        {
            IsProjectsLoaded = Projects.All(project => project.Status == LoadingStatus.Loaded);
            OnProjectsLoaded?.Invoke();
        }

        public override string ToString()
        {
            return this.SolutionFile.Name;
        }
        private void InitializeFileWatcher()
        {
            _watcher.Path = this.SolutionFile.DirectoryName;
            _watcher.Filter = this.SolutionFile.Name;
            _watcher.EnableRaisingEvents = true;
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;

            _watcher.Changed += new FileSystemEventHandler(Watcher_Changed);
        }
    }
}
