using System;
using System.Linq;

namespace FoxNuGet.VSNuGet
{
    public sealed class VSNugetFile
    {
        public string Source { get; }
        public string Target { get; }

        public VSNugetFile(string sourceFilePath, string targetFilePath)
        {
            this.Source = sourceFilePath ?? throw new ArgumentNullException(nameof(sourceFilePath));
            this.Target = targetFilePath ?? throw new ArgumentNullException(nameof(targetFilePath));
        }

        public override string ToString()
        {
            return Source.Replace("\\", "/").Split('/').Last();
        }
    }
}