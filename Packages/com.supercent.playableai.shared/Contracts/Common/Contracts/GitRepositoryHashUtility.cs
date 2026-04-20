using System;
using System.IO;

namespace Supercent.PlayableAI.Common.Contracts
{
    public static class GitRepositoryHashUtility
    {
        public static bool TryGetHeadCommitHash(string repositoryRootPath, out string commitHash)
        {
            commitHash = string.Empty;
            string repositoryRoot = NormalizeFullPath(repositoryRootPath);
            if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot))
                return false;

            if (!TryResolveGitDirectory(repositoryRoot, out string gitDirectory))
                return false;

            string headPath = Path.Combine(gitDirectory, "HEAD");
            if (!File.Exists(headPath))
                return false;

            string headText = Normalize(File.ReadAllText(headPath));
            if (string.IsNullOrWhiteSpace(headText))
                return false;

            if (!headText.StartsWith("ref:", StringComparison.Ordinal))
            {
                commitHash = headText;
                return !string.IsNullOrWhiteSpace(commitHash);
            }

            string relativeRef = Normalize(headText.Substring(4));
            if (string.IsNullOrWhiteSpace(relativeRef))
                return false;

            string refPath = Path.Combine(gitDirectory, relativeRef.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(refPath))
            {
                commitHash = Normalize(File.ReadAllText(refPath));
                return !string.IsNullOrWhiteSpace(commitHash);
            }

            string packedRefsPath = Path.Combine(gitDirectory, "packed-refs");
            if (!File.Exists(packedRefsPath))
                return false;

            string targetSuffix = " " + relativeRef;
            foreach (string line in File.ReadLines(packedRefsPath))
            {
                string normalizedLine = Normalize(line);
                if (string.IsNullOrWhiteSpace(normalizedLine) ||
                    normalizedLine.StartsWith("#", StringComparison.Ordinal) ||
                    normalizedLine.StartsWith("^", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!normalizedLine.EndsWith(targetSuffix, StringComparison.Ordinal))
                    continue;

                int separatorIndex = normalizedLine.IndexOf(' ');
                if (separatorIndex <= 0)
                    continue;

                commitHash = Normalize(normalizedLine.Substring(0, separatorIndex));
                return !string.IsNullOrWhiteSpace(commitHash);
            }

            return false;
        }

        private static bool TryResolveGitDirectory(string repositoryRoot, out string gitDirectory)
        {
            gitDirectory = string.Empty;
            string dotGitPath = Path.Combine(repositoryRoot, ".git");
            if (Directory.Exists(dotGitPath))
            {
                gitDirectory = dotGitPath;
                return true;
            }

            if (!File.Exists(dotGitPath))
                return false;

            string pointer = Normalize(File.ReadAllText(dotGitPath));
            const string prefix = "gitdir:";
            if (!pointer.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string relativeGitDirectory = Normalize(pointer.Substring(prefix.Length));
            if (string.IsNullOrWhiteSpace(relativeGitDirectory))
                return false;

            string resolved = Path.GetFullPath(Path.Combine(repositoryRoot, relativeGitDirectory));
            if (!Directory.Exists(resolved))
                return false;

            gitDirectory = resolved;
            return true;
        }

        private static string NormalizeFullPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return Path.GetFullPath(value)
                .Trim()
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
