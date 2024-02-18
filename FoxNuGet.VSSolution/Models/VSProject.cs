using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FoxNuGet.VSSolution
{
    public sealed class VSProject : IVSVersionableResource
    {
        private readonly List<IVSVersionableResource> _references = new List<IVSVersionableResource>();
        private readonly IList<FileInfo> _missingReferences = new ObservableCollection<FileInfo>();
        private LoadingStatus _status;
        private Version _version = new Version(1, 0, 0);
        private readonly FileSystemWatcher _watcher = new FileSystemWatcher();

        public VSProject(string uniqueId, string name, FileInfo projectFile, VSSolution ownerSolution)
        {
            if (string.IsNullOrEmpty(uniqueId))
            {
                throw new ArgumentException($"« {nameof(uniqueId)} » ne peut pas être vide ou avoir la valeur Null.", nameof(uniqueId));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"« {nameof(name)} » ne peut pas être vide ou avoir la valeur Null.", nameof(name));
            }

            OwnerSolution = ownerSolution;
            OwnerSolution.OnProjectsLoaded += OwnerSolution_OnProjectsLoaded;
            UniqueId = uniqueId;
            Name = name;
            AssemblyName = $"{name}.dll";
            ProjectFile = projectFile ?? throw new ArgumentNullException(nameof(projectFile));
            ResourceType = SolutionResourceType.Project;
            (_missingReferences as ObservableCollection<FileInfo>).CollectionChanged += VSProject_CollectionChanged;
            LoadAsync().Wait();
            InitializeFileWatcher();
        }

        private void OwnerSolution_OnProjectsLoaded()
        {
            RefreshMissingReferences();
        }

        public VSProject(string uniqueId, string name, FileInfo projectFile) : this (uniqueId, name, projectFile, ownerSolution: null)
        {
            
        }

        private void VSProject_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (OwnerSolution is null)
                return;

            OwnerSolution.OnSolutionFileChanged -= OwnerSolution_OnSolutionFileModified;
            if (_missingReferences.Any())
            {
                OwnerSolution.OnSolutionFileChanged += OwnerSolution_OnSolutionFileModified;
            }

            OnWarningSent?.Invoke(this);
        }

        private void OwnerSolution_OnSolutionFileModified(VSSolution obj)
        {
            RefreshMissingReferences();
        }

        private void RefreshMissingReferences()
        {
            List<FileInfo> missingReferences = _missingReferences.ToList();
            _missingReferences.Clear();
            foreach (FileInfo projectFile in missingReferences)
            {
                AddProjectReference(projectFile.FullName);
            }
        }

        public string PackageTags { get; private set; }

        public LoadingStatus Status
        {
            get => _status; private set
            {
                _status = value;
                OnStatusChanged?.Invoke(this);
            }
        }
        public SolutionResourceType ResourceType { get; }

        public string UniqueId { get; }

        public string Name { get; }

        public string AssemblyName { get; private set; }

        public string Description { get; set; }

        public Version Version
        {
            get => _version;
            set
            {
                if (_version == value)
                    return;

                string currentTag = $"<AssemblyVersion>{_version}</AssemblyVersion>";
                string newTag = $"<AssemblyVersion>{value}</AssemblyVersion>";
                string rawdata = File.ReadAllText(ProjectFile.FullName);
                if (rawdata.Contains(currentTag))
                {
                    rawdata = rawdata.Replace(currentTag, newTag);
                }
                else
                {
                    rawdata = rawdata.Insert(rawdata.IndexOf("</PropertyGroup>"), Environment.NewLine + newTag + Environment.NewLine);
                }
                File.WriteAllText(ProjectFile.FullName, rawdata);
                _version = value;
            }
        }
        public VSSolution OwnerSolution { get; internal set; }

        public IEnumerable<IVSVersionableResource> References => _references;
        public FileInfo ProjectFile { get; }
        public bool GenerateDocumentationFile { get; private set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string ProjectUrl { get; private set; }
        public string Copyright { get; private set; }

        public event Action<VSProject> OnWarningSent;

        public event Action<VSProject> OnStatusChanged;
        public event Action<VSProject> OnProjectFileChanged;

        public async Task LoadAsync()
        {
            string assemblyVersionTag = "AssemblyVersion";
            string documentationFileTag = "GenerateDocumentationFile";
            string packageReferenceTag = "PackageReference";
            string projectReferenceTag = "ProjectReference";
            string assemblyNameTag = "AssemblyName";
            string titleTag = "Title";
            string authorTag = "Author";
            string projectUrlTag = "PackageProjectUrl";
            string copyrightTag = "Copyright";
            string packageTagsTag = "PackageTags";

            Status = LoadingStatus.Loading;
            foreach (string line in File.ReadAllLines(ProjectFile.FullName))
            {
                if (line.Contains($"<{assemblyVersionTag}>"))
                {
                    string value = line.Trim().Replace($"<{assemblyVersionTag}>", string.Empty).Replace($"</{assemblyVersionTag}>", string.Empty);
                    if (Version.TryParse(value, out Version version))
                        _version = version;
                }
                else
                if (line.Contains($"<{documentationFileTag}>"))
                {
                    string value = line.Replace($"<{documentationFileTag}>", string.Empty).Replace($"</{documentationFileTag}>", string.Empty).Trim();
                    if (bool.TryParse(value, out bool docGenerated))
                        GenerateDocumentationFile = docGenerated;
                }
                else
                if (line.Contains($"<{assemblyNameTag}>"))
                {
                    string value = line.Replace($"<{assemblyNameTag}>", string.Empty).Replace($"</{assemblyNameTag}>", string.Empty).Trim();
                    AssemblyName = $"{value.Replace("$(MSBuildProjectName)", Name)}.dll";
                }
                else
                if (line.Contains($"<{titleTag}>"))
                {
                    Title = line.Replace($"<{titleTag}>", string.Empty).Replace($"</{titleTag}>", string.Empty).Trim();
                }
                else
                if (line.Contains($"<{authorTag}>"))
                {
                    Author = line.Replace($"<{authorTag}>", string.Empty).Replace($"</{assemblyNameTag}>", string.Empty).Trim();
                }
                else
                if (line.Contains($"<{projectUrlTag}>"))
                {
                    ProjectUrl = line.Replace($"<{projectUrlTag}>", string.Empty).Replace($"</{projectUrlTag}>", string.Empty).Trim();
                }
                else
                if (line.Contains($"<{copyrightTag}>"))
                {
                    Copyright = line.Replace($"<{copyrightTag}>", string.Empty).Replace($"</{copyrightTag}>", string.Empty).Trim();
                }
                else
                if (line.Contains($"<{packageTagsTag}>"))
                {
                    PackageTags = line.Replace($"<{packageTagsTag}>", string.Empty).Replace($"</{packageTagsTag}>", string.Empty).Trim();
                }
                else
                if (line.Contains($"<{packageReferenceTag} "))
                {
                    string value = line.Replace($"<{packageReferenceTag}", string.Empty).Replace($"/>", string.Empty).Trim();
                    string[] attributes = value.Split(' ');
                    string packageName = null;
                    Version packageVersion = null;
                    foreach (string attribute in attributes.Select(a => a.Replace("\"", string.Empty)))
                    {
                        if (attribute.StartsWith("Include="))
                        {
                            packageName = attribute.Split('=')[1];
                        }
                        else
                        if (attribute.StartsWith("Version="))
                        {
                            if (!Version.TryParse(attribute.Split('=')[1], out packageVersion))
                                packageVersion = new Version(1, 0, 0);
                        }
                        if (packageName != null && packageVersion != null)
                            _references.Add(new VSPackage(packageName, packageVersion));
                    }
                }
                else
                if (line.Contains($"<{projectReferenceTag} "))
                {
                    string value = line.Replace($"<{projectReferenceTag}", string.Empty).Replace($"/>", string.Empty).Trim();
                    string[] attributes = value.Split(' ');
                    string projectPath = null;
                    foreach (string attribute in attributes.Select(a => a.Replace("\"", string.Empty)))
                    {
                        if (attribute.StartsWith("Include="))
                        {
                            projectPath = attribute.Split('=')[1];
                            AddProjectReference(projectPath);
                        }
                    }
                }
            }
            Status = LoadingStatus.Loaded;
        }

        public IEnumerable<string> ShowWarning()
        {
            List<string> warning = new List<string>();
            foreach (FileInfo missingFile in _missingReferences)
            {
                warning.Add($"{Name} | This file missed in the current solution \"{missingFile.Name}\".");
            }
            return warning;
        }

        public override string ToString()
        {
            return this.ProjectFile.Name;
        }

        private void AddProjectReference(string projectPath)
        {
            string projectName;
            FileInfo projectFile = new FileInfo(projectPath);
            projectName = projectFile.Name.Replace(projectFile.Extension, string.Empty);
            VSProject solutionProject = OwnerSolution?.Projects.FirstOrDefault(project => project.Name.Equals(projectName));
            if (solutionProject != null)
                _references.Add(solutionProject);
            else
                _missingReferences.Add(projectFile);
        }

        private void InitializeFileWatcher()
        {
            _watcher.Path = this.ProjectFile.DirectoryName;
            _watcher.Filter = this.ProjectFile.Name;
            _watcher.EnableRaisingEvents = true;
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;

            _watcher.Changed += new FileSystemEventHandler(Watcher_Changed);
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            OnProjectFileChanged?.Invoke(this);
        }
    }
}