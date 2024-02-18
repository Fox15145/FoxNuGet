using LibGit2Sharp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FoxNuGet.VSGit
{
    public class VSGit
    {
        public VSGit(DirectoryInfo localRepositoryPath)
        {
            LocalRepositoryPath = localRepositoryPath ?? throw new ArgumentNullException(nameof(localRepositoryPath));

            LocalRepositoryPath = GitHelpers.FindRepositoryPath(LocalRepositoryPath);
            if (LocalRepositoryPath == null)
            {
                throw new DirectoryNotFoundException("No repository found.");
            }

            if (LocalRepositoryPath.GetDirectories(".git", SearchOption.TopDirectoryOnly).Any())
                GetModifiedFilesOnCurrentBranch(LocalRepositoryPath);
        }

        public DirectoryInfo LocalRepositoryPath { get; }
        public Commit CommonAncestorCommit { get; private set; }
        public IEnumerable<Commit> CommitLog { get; private set; }
        public IEnumerable<string> Messages { get; private set; }
        public IEnumerable<FileInfo> ModifiedFiles { get; private set; }
        public Branch MasterBranch { get; private set; }
        public Branch CurrentBranch { get; private set; }

        private static IEnumerable<FileInfo> GetModifiedFiles(DirectoryInfo directoryInfo, string commitHash)
        {
            using (var repo = new Repository(directoryInfo.FullName))
            {
                var commit = repo.Lookup<Commit>(commitHash);

                // Get the tree for the commit
                var tree = commit.Tree;

                // Get the parent commit
                var parent = commit.Parents.FirstOrDefault();

                if (parent != null)
                {
                    // Get the tree for the parent commit
                    Tree parentTree = parent.Tree;

                    // Get the changes between the commit tree and parent tree
                    TreeChanges changes = repo.Diff.Compare<TreeChanges>(parentTree, tree);

                    // Get the renamed files
                    IEnumerable<FileInfo> renamedFiles = changes.Renamed.Select(x => new FileInfo(Path.Combine(directoryInfo.FullName, x.Path)));

                    // Get the modified files
                    IEnumerable<FileInfo> modifiedFiles = changes.Modified.Select(x => new FileInfo(Path.Combine(directoryInfo.FullName, x.Path)));

                    // Get the added files
                    IEnumerable<FileInfo> addedFiles = changes.Added.Select(x => new FileInfo(Path.Combine(directoryInfo.FullName, x.Path)));

                    // Get the removed files
                    IEnumerable<FileInfo> removedFiles = changes.Deleted.Select(x => new FileInfo(Path.Combine(directoryInfo.FullName, x.Path)));

                    return modifiedFiles.Concat(addedFiles).Concat(removedFiles).Concat(renamedFiles);
                }
                else
                {
                    Console.WriteLine("No parent commit exists. This is the first commit.");
                }
                return default;
            }
        }

        private void GetModifiedFilesOnCurrentBranch(DirectoryInfo directoryInfo)
        {
            using (var repo = new Repository(directoryInfo.FullName))
            {
                // Access the master branch
                MasterBranch = repo.Branches["master"];

                // Access the current branch
                CurrentBranch = repo.Head;

                // Find the common ancestor commit
                CommonAncestorCommit = repo.ObjectDatabase.FindMergeBase(MasterBranch.Tip, CurrentBranch.Tip);

                // Get the commit history starting from the common ancestor commit
                var commitFilter = new CommitFilter
                {
                    IncludeReachableFrom = CurrentBranch.Tip,
                    ExcludeReachableFrom = CommonAncestorCommit
                };

                CommitLog = repo.Commits.QueryBy(commitFilter).ToList();
                Messages = CommitLog.Select(c => c.Message).ToList();

                foreach (Commit commit in CommitLog)
                {
                    // Process each commit
                    if (ModifiedFiles is null)
                    {
                        ModifiedFiles = new List<FileInfo>(GetModifiedFiles(directoryInfo, commit.Sha));
                    }
                    else
                    {
                        (ModifiedFiles as List<FileInfo>)?.AddRange(GetModifiedFiles(directoryInfo, commit.Sha));
                    }
                }
            }
        }
    }
}