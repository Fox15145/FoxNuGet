using Microsoft.Extensions.Logging;

using NuGet.Packaging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace FoxNuGet.VSNuGet
{
    public sealed class VSNuSpec : NuspecReader
    {
        private const string Src = "src";
        private const string Target = "target";

        public VSNuSpec(string path) : base(path)
        {
            NuSpecFile = new FileInfo(path);
            UniqueId = GenerateUniqueId(NuSpecFile);
        }

        public ILogger Logger { private get; set; }

        public static string GenerateUniqueId(FileInfo nuSpecFile)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(nuSpecFile.FullName);

            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }

        public FileInfo NuSpecFile { get; }
        public string UniqueId { get; }

        public bool IsReady
        {
            get
            {
                bool isOk = true;
                if (!NuSpecFile.Exists)
                {
                    Logger?.LogError("!NuSpecFile.Exists");
                    isOk = false;
                }
                if (string.IsNullOrWhiteSpace(this.GetId().Replace("$id$", string.Empty)))
                {
                    Logger?.LogError("string.IsNullOrWhiteSpace(this.GetId().Replace(\"$id$\", string.Empty))");
                    isOk = false;
                }
                if (string.IsNullOrWhiteSpace(this.GetAuthors().Replace("$author$", string.Empty)))
                {
                    Logger?.LogError("string.IsNullOrWhiteSpace(this.GetAuthors().Replace(\"$author$\", string.Empty))");
                    isOk = false;
                }
                if (string.IsNullOrWhiteSpace(this.GetDescription().Replace("$author$", string.Empty)))
                {
                    Logger?.LogError("string.IsNullOrWhiteSpace(this.GetDescription().Replace(\"$author$\", string.Empty))");
                    isOk = false;
                }
                if (string.IsNullOrWhiteSpace(this.GetTitle().Replace("$title$", string.Empty)))
                {
                    Logger?.LogError("string.IsNullOrWhiteSpace(this.GetTitle().Replace(\"$title$\", string.Empty))");
                    isOk = false;
                }
                if (string.IsNullOrWhiteSpace(this.GetSummary().Replace("Summary of changes made in this release of the package.", string.Empty)))
                {
                    Logger?.LogError("string.IsNullOrWhiteSpace(this.GetSummary().Replace(\"Summary of changes made in this release of the package.\", string.Empty))");
                    isOk = false;
                }
                if (string.IsNullOrWhiteSpace(this.GetCopyright().Replace("$copyright$", string.Empty)))
                {
                    Logger?.LogError("string.IsNullOrWhiteSpace(this.GetCopyright().Replace(\"$copyright$\", string.Empty))");
                    isOk = false;
                }
                if (string.IsNullOrWhiteSpace(this.GetTags().Replace("Tag1 Tag2", string.Empty)))
                {
                    Logger?.LogError("string.IsNullOrWhiteSpace(this.GetTags().Replace(\"Tag1 Tag2\", string.Empty))");
                    isOk = false;
                }
                if (!GetDependencyGroups().Any())
                {
                    Logger?.LogError("Tag DependencyGroups not found in the nuspec file.");
                    isOk = false;
                }
                    return isOk;

                //string.IsNullOrWhiteSpace(this.GetProjectUrl().Replace("http://project_url_here_or_delete_this_line/", string.Empty))
                //this.GetContentFiles().Any()

            }
        }
        public IEnumerable<VSNugetFile> GetFiles()
        {
            return this.Xml.Descendants("file")
                .Where(f => f.Attribute(Src) != null && f.Attribute(Target) != null)
                .Select(f => CreateNewVSNuGetFile(f));
        }

        private static VSNugetFile CreateNewVSNuGetFile(XElement fileElement)
        {
            if (fileElement == null)
                throw new ArgumentNullException(nameof(fileElement));

            string sourceFilePath = fileElement.Attribute(Src)?.Value;
            string targetFilePath = fileElement.Attribute(Target)?.Value;
            return sourceFilePath != null && targetFilePath != null
                ? new VSNugetFile(sourceFilePath, targetFilePath)
                : throw new ArgumentNullException(); ;
        }
    }
}
