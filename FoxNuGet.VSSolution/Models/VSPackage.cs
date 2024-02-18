using System;

namespace FoxNuGet.VSSolution
{
    public sealed class VSPackage : IVSVersionableResource
    {
        public VSPackage(string packageName, Version packageVersion)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                throw new ArgumentException($"« {nameof(packageName)} » ne peut pas être vide ou avoir la valeur Null.", nameof(packageName));
            }

            Name = packageName;
            Version = packageVersion ?? throw new ArgumentNullException(nameof(packageVersion));
        }

        public object PackageName { get; }
        public object PackageVersion { get; }

        public string Name { get; }

        public SolutionResourceType ResourceType => SolutionResourceType.Package;

        public Version Version { get; }
    }
}