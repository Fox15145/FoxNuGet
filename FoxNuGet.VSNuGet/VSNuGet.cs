using NuGet.Packaging;
using NuGet.Packaging.Core;

using FoxNuGet.VSSolution;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FoxNuGet.VSNuGet.Properties;
using System.Text;
using NuGet.Versioning;
using FoxNuGet.VSNuget.Models;
using Microsoft.Extensions.Logging;

namespace FoxNuGet.VSNuGet
{
    public sealed class VSNuGet
    {
        private string _configurationMode;
        private bool _nuSpecCreated;

        //private readonly string _nugetToolPath = Path.Combine("Resources", "nuget.exe");

        public VSNuGet(VSProject vsProject, DirectoryInfo outputDirectory, ILogger logger = null)
        {
            Project = vsProject ?? throw new ArgumentNullException(nameof(vsProject));
            OutputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
            Logger = logger;
            string solutionPath = Project.OwnerSolution?.SolutionFile?.DirectoryName ?? string.Empty;
            string projectExtention = Project.ProjectFile.Extension;
            string nuspecFilename = Project.ProjectFile.Name.Replace(projectExtention, ".nuspec");
            string nuspecPath = Path.Combine(solutionPath, "NuSpecs", nuspecFilename);
            FileInfo nuspecFile = new FileInfo(nuspecPath);

            NugetToolFile = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FoxNuGet", "nuget.exe"));

            CreateEmptyNuSpecIfMissing(nuspecFile);

            Nuspec = new VSNuSpec(nuspecPath);
            Nuspec.Logger = Logger;

            if (_nuSpecCreated)
                LoadReferencesFromProject();

            if (!NugetToolFile.Exists)
            {
                Directory.CreateDirectory(NugetToolFile.Directory.FullName);
                File.WriteAllBytes(NugetToolFile.FullName, Resources.nuget);
            }
        }

        public ILogger Logger { private get; set; }

        private void LoadReferencesFromProject()
        {


            //// Read the .nuspec file
            //NuspecReader reader = new NuspecReader("path/to/package.nuspec");

            //// Get the list of existing files
            //List<PackageFile> files = reader.GetFiles().ToList();

            //// Create a new PackageFile entry for the file you want to add
            //PackageFile newFile = new PackageFile
            //{
            //    Source = "path/to/source/file.txt",
            //    Target = "content/destination/file.txt"
            //};

            //// Add the new file to the list of files
            //files.Add(newFile);

            //// Update the files in the NuspecReader
            //reader = new NuspecReader(reader.GetNuspec(), files);

            //// Write the modified NuspecReader back to the .nuspec file
            //reader.Save("path/to/package.nuspec");

        }

        //public void GenerateNuspec(string csprojFilePath, string nuspecFilePath)
        //{
        //    NuspecReader reader = new NuspecReader(csprojFilePath);
        //    PackageBuilder builder = new PackageBuilder(reader.GetMetadata(), new[] { reader.GetMetadata().Id });
        //    builder.PopulateFiles(csprojFilePath);

        //    using (var writer = new StreamWriter(nuspecFilePath))
        //    {
        //        builder.Save(writer);
        //    }
        //}

        private void CreateEmptyNuSpecIfMissing(FileInfo nuspecFile)
        {
            if (nuspecFile is null)
            {
                throw new ArgumentNullException(nameof(nuspecFile));
            }

            if (!nuspecFile.Exists && NugetToolFile.Exists)
            {
                ExecuteNugetCommand($"spec {Project.AssemblyName}");
                FileInfo tempNugetFile = new FileInfo(Project.ProjectFile.FullName.Replace(Project.ProjectFile.Extension, nuspecFile.Extension));
                if (!tempNugetFile.Exists)
                    return;

                Directory.CreateDirectory(nuspecFile.Directory.FullName);
                tempNugetFile.MoveTo(nuspecFile.FullName);

                StringBuilder description = new StringBuilder(Project.Description);
                description.AppendLine(Project.AssemblyName);
                if (Project.References.Any(r => r is VSProject))
                    description.AppendLine(string.Join(Environment.NewLine, Project.References.Where(r => r is VSProject).Select(p => (p as VSProject).AssemblyName)));

                string nuSpecContent = File.ReadAllText(nuspecFile.FullName);
                nuSpecContent = nuSpecContent.Replace("$id$", Project.Name);
                nuSpecContent = nuSpecContent.Replace("$title$", Project.Title);
                nuSpecContent = nuSpecContent.Replace("$author$", Project.Author);
                //nuSpecContent = nuSpecContent.Replace("http://project_url_here_or_delete_this_line/", VsProject.ProjectUrl);
                nuSpecContent = nuSpecContent.Replace("$description$", description.ToString());
                nuSpecContent = nuSpecContent.Replace("Summary of changes made in this release of the package.", "");
                nuSpecContent = nuSpecContent.Replace("$copyright$", Project.Copyright);
                nuSpecContent = nuSpecContent.Replace("Tag1 Tag2", Project.PackageTags);

                File.WriteAllText(nuspecFile.FullName, nuSpecContent);
                _nuSpecCreated = true;
            }
        }

        private void ExecuteNugetCommand(string arguments = "pack")
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo(NugetToolFile.FullName)
            {
                WorkingDirectory = Project.ProjectFile.DirectoryName,
                Arguments = arguments,
                //UseShellExecute = false,
                //RedirectStandardOutput = true,
                //RedirectStandardError = true,
            };
            Process process = new Process();
            process.StartInfo = processStartInfo;
            //process.OutputDataReceived += (s, e) => { Logger?.LogInformation(8, $"nuget.exe output: {e.Data}"); };
            //process.ErrorDataReceived += (s, e) => { Logger?.LogError(8, $"nuget.exe output: {e.Data}"); };

            process.Start();

            //process.BeginOutputReadLine();
            //process.BeginErrorReadLine();

            process.WaitForExit();
        }

        public VSProject Project { get; }
        public DirectoryInfo OutputDirectory { get; }

        public VSNuSpec Nuspec { get; }
        public FileInfo NugetToolFile { get; }
        public bool AutomaticVersionIncrement { get; set; } = true;
        public VSPreview VsPreview { get; private set; } = new VSPreview();

        /// <summary>
        /// Generate a new nuget package from current configuration mode.
        /// If ConfgurationMode is "DEBUG" a nuget with 4 digits version will be generate;
        /// 3 digits will be use in "RELEASE" configuration mode.
        /// Example : 
        /// DEBUG MODE = 1.0.0.10
        /// RELEASE MODE = 1.0.0
        /// </summary>
        /// <param name="versionToGenerate"></param>
        /// <exception cref="ArgumentException"></exception>
        public void Generate(NuGetVersion versionToGenerate)
        {
            if (string.IsNullOrEmpty(_configurationMode))
            {
                throw new ArgumentException($"« {nameof(_configurationMode)} » ne peut pas être vide ou avoir la valeur Null. Utilisez la méthode \"Prepare()\" pour l'initialiser.", nameof(_configurationMode));
            }

            if (!Nuspec.IsReady)
                return;

            ChangeConfigurationTarget(_configurationMode);
            AddXmlDocumentation();
            AddDebugFilePDB(_configurationMode);
            List<string> arguments = BuildNugetCommandArguments(versionToGenerate);

            ExecuteNugetCommand(string.Join(" ", arguments));
        }

        public NuGetVersion Prepare(string configurationMode)
        {
            return Prepare(configurationMode, null, null);
        }

        public NuGetVersion Prepare(string configurationMode, IEnumerable<VSProject> impactedProjects, IEnumerable<string> releaseNotes)
        {
            _configurationMode = null;
            if (string.IsNullOrEmpty(configurationMode))
            {
                var ex = new ArgumentException($"« {nameof(configurationMode)} » ne peut pas être vide ou avoir la valeur Null.", nameof(configurationMode));
                Logger?.LogError(ex, ex.Message);
                throw ex;
            }

            if (!Nuspec.IsReady)
            {
                Logger?.LogWarning($"{Nuspec.NuSpecFile.Name} ({configurationMode}) is not ready.");
                return null;
            }

            NuGetVersion versionToGenerate = GetVersionToGenerate(configurationMode);

            if (impactedProjects is null)
                ApplyVersionInRelatedProjects(versionToGenerate);
            else
                ApplyVersionInRelatedProjects(versionToGenerate, impactedProjects);

            ConsolidateNuspecsVersions();
            ChangePackageDescriptionInCurrentNuspec();

            if (!(releaseNotes is null))
                ChangePackageReleaseNotesInCurrentNuspec(releaseNotes);

            _configurationMode = configurationMode;
            return versionToGenerate;
        }

        private void AddXmlDocumentation()
        {
            if (!Project.GenerateDocumentationFile)
                return;

            IEnumerable<XElement> files = GetXElementsFromNuspec();

            if (files.Any(f => f.Attribute("src").Value.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                return;

            foreach (XElement file in files.Where(f => f.Attribute("src").Value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                string xmlSrcFile = file.Attribute("src")?.Value.Replace(".dll", ".xml");
                string xmlTargetFile = file.Attribute("target")?.Value.Replace(".dll", ".xml");

                if (File.Exists(xmlSrcFile))
                {
                    XAttribute xAttributeSrcFile = new XAttribute("src", xmlSrcFile);
                    XAttribute xAttributeTargetFile = new XAttribute("target", xmlTargetFile);
                    XElement xElementDocFile = new XElement("file", xAttributeSrcFile, xAttributeTargetFile);
                    Nuspec.Xml.Descendants("files").FirstOrDefault()?.Add(xElementDocFile);
                }
            }

            Nuspec.Xml.Save(Nuspec.NuSpecFile.FullName);
        }

        private void AddDebugFilePDB(string configurationMode)
        {
            if (!configurationMode.Equals("Debug", StringComparison.OrdinalIgnoreCase))
                return;

            IEnumerable<XElement> files = GetXElementsFromNuspec();

            // Remove PDB files.
            files.Where(f => f.Attribute("src").Value.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)).Remove();

            foreach (XElement file in files.Where(f => f.Attribute("src").Value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                string pdbSrcFile = file.Attribute("src")?.Value.Replace(".dll", ".pdb");
                string pdbTargetFile = file.Attribute("target")?.Value.Replace(".dll", ".pdb");

                if (File.Exists(pdbSrcFile))
                {
                    XAttribute xAttributeSrcFile = new XAttribute("src", pdbSrcFile);
                    XAttribute xAttributeTargetFile = new XAttribute("target", pdbTargetFile);
                    XElement xElementDocFile = new XElement("file", xAttributeSrcFile, xAttributeTargetFile);
                    Nuspec.Xml.Descendants("files").FirstOrDefault()?.Add(xElementDocFile);
                }
            }

            Nuspec.Xml.Save(Nuspec.NuSpecFile.FullName);
        }

        private List<string> BuildNugetCommandArguments(NuGetVersion versionToGenerate)
        {
            return new List<string>()
            {
                "pack",
                $"\"{Nuspec.NuSpecFile.FullName}\"",
                $"-Version {versionToGenerate}",
                $"-OutputDirectory \"{OutputDirectory.FullName}\""
            };
        }

        private void ChangeConfigurationTarget(string configurationMode)
        {
            IEnumerable<XElement> files = GetXElementsFromNuspec();

            string ActualConfig = "Release";
            if (files.Any(f => f.Attribute("src").Value.Replace("\\", "/").ToLower().Contains("/Debug/")))
            {
                ActualConfig = "Debug";
            }

            foreach (XElement file in files)
            {
                file.Attribute("src").Value = file.Attribute("src").Value.Replace("\\", "/");
                file.Attribute("src").Value = file.Attribute("src").Value.Replace(ActualConfig, configurationMode);
            }

            Nuspec.Xml.Save(Nuspec.NuSpecFile.FullName);
        }

        private IEnumerable<XElement> GetXElementsFromNuspec()
        {
            IEnumerable<XElement> files = Nuspec.Xml.Descendants("file");
            files = files.Where(f => f.Attribute("src") != null && f.Attribute("target") != null);
            return files;
        }

        private void ApplyVersionInRelatedProjects(NuGetVersion versionToGenerate)
        {
            ApplyVersionInRelatedProjects(versionToGenerate, GetIncludedProjects());
        }

        private void ApplyVersionInRelatedProjects(NuGetVersion versionToGenerate, IEnumerable<VSProject> impactedProjects)
        {
            Project.Version = versionToGenerate.Version;
            foreach (VSProject project in GetIncludedProjects().Intersect(impactedProjects))
            {
                project.Version = versionToGenerate.Version;
            }
        }

        /// <summary>Gets projects references to include in the nuget of the current <see cref="Project"/>.</summary>
        /// <returns>A list of <see cref="VSProject"/> that match between project references and existing nuspec.</returns>
        public IEnumerable<VSProject> GetIncludedProjects()
        {
            HashSet<VSProject> includedProjects = new HashSet<VSProject>();
            foreach (VSNugetFile file in Nuspec.GetFiles())
            {
                string filenameWithoutExtension = file.ToString().Replace(".dll", string.Empty);
                IVSVersionableResource reference = Project.References.FirstOrDefault(p => p.Name.Equals(filenameWithoutExtension, StringComparison.OrdinalIgnoreCase));
                if (reference is VSProject vsproject)
                    includedProjects.Add(vsproject);
            }
            return includedProjects;
        }

        /// <summary>
        /// Checks the nugets versions expected in the nuspec versus versions in the csproj ;
        /// Version in nuspec will be updated from these one of the csproj.
        /// </summary>
        /// <param name="versionToGenerate"></param>
        private void ConsolidateNuspecsVersions()
        {
            foreach (PackageDependencyGroup frameworkSpecificGroup in this.Nuspec.GetDependencyGroups())
            {
                foreach (PackageDependency package in frameworkSpecificGroup.Packages)
                {
                    IVSVersionableResource reference = Project.References.FirstOrDefault(r => r is VSPackage && r.Name.Contains(package.Id));
                    if (reference is null)
                        continue;

                    ChangePackageVersionInCurrentNuspec(package, reference);
                }
            }
        }

        private void ChangePackageVersionInCurrentNuspec(PackageDependency package, IVSVersionableResource reference)
        {
            if (reference is null || package is null)
            {
                return;
            }

            if (reference.Version == package.VersionRange.MinVersion.Version)
            {
                return;
            }

            string packageVersion = package.VersionRange.MinVersion.ToString();
            IEnumerable<XElement> elements = Nuspec.Xml.Descendants("dependency").Where(e =>
                                        e.HasAttributes
                                        && e.Attribute("id").Value.Equals(package.Id, StringComparison.Ordinal)
                                        && e.Attribute("version").Value.Equals(packageVersion, StringComparison.Ordinal));

            foreach (XElement element in elements)
            {
                var versionAttribute = element.Attribute("version");
                if (versionAttribute != null)
                {
                    versionAttribute.Value = reference.Version.ToString();
                }
            }
            Nuspec.Xml.Save(Nuspec.NuSpecFile.FullName);
        }

        private void ChangePackageReleaseNotesInCurrentNuspec(IEnumerable<string> releaseNotes)
        {
            if (releaseNotes is null)
            {
                return;
            }

            XElement releaseNotesAttribute = Nuspec.Xml.Descendants("releaseNotes").FirstOrDefault();
            if (releaseNotesAttribute is null)
                return;

            releaseNotesAttribute.Value = string.Join(Environment.NewLine, releaseNotes.Select(n => n.TrimEnd()));

            Nuspec.Xml.Save(Nuspec.NuSpecFile.FullName);
        }

        private void ChangePackageDescriptionInCurrentNuspec()
        {
            XElement descriptionAttribute = Nuspec.Xml.Descendants("description").FirstOrDefault();
            if (descriptionAttribute is null)
                return;

            descriptionAttribute.Value = string.Join(Environment.NewLine, Nuspec.GetFiles().Select(f => Path.GetFileName(f.Source)));

            Nuspec.Xml.Save(Nuspec.NuSpecFile.FullName);            
        }

        public NuGetVersion GetVersionToGenerate(string ConfigurationMode)
        {
            NuGetVersion versionToGenerate;

            if (AutomaticVersionIncrement)
            {
                bool isDebugConfiguration = ConfigurationMode.Equals("DEBUG", StringComparison.OrdinalIgnoreCase);
                int vMajor = Project.Version.Major;
                int vMinor = Project.Version.Minor;
                int vBuild = isDebugConfiguration ? Project.Version.Build : Project.Version.Build + 1;
                int vRevision = -1;
                if (isDebugConfiguration)
                    vRevision = Project.Version.Revision < 10 ? 10 : Project.Version.Revision + 1;

                if (isDebugConfiguration && !VsPreview.Active)
                    versionToGenerate = new NuGetVersion(vMajor, vMinor, vBuild, vRevision);
                else
                 if (isDebugConfiguration && VsPreview.Active)
                    versionToGenerate = new NuGetVersion(vMajor, vMinor, vBuild, $"-{VsPreview.Pattern}.{vRevision}");
                else
                    versionToGenerate = new NuGetVersion(vMajor, vMinor, vBuild);
            }
            else
                versionToGenerate = new NuGetVersion(Project.Version);
            return versionToGenerate;
        }
    }
}