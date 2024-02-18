// See https://aka.ms/new-console-template for more information
using System;
using System.IO;
using System.Linq;

namespace FoxNuGet.VSGit
{
    internal static class GitHelpers
    {
        public static DirectoryInfo FindRepositoryPath(DirectoryInfo directoryInfo)
        {
            if (directoryInfo is null)
                throw new ArgumentNullException(nameof(directoryInfo));

            if (directoryInfo.GetDirectories(".git", SearchOption.AllDirectories)?.Any() == false)
                return FindRepositoryPath(directoryInfo.Parent);
            else
                return directoryInfo;
        }
    }
}
