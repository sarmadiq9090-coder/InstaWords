using System;
using System.IO;

namespace InstaWords.Core.Tests
{
    /// <summary>Paths in the repository, found from the test binaries upwards.</summary>
    internal static class Repo
    {
        internal static readonly string Root = FindRoot();

        private static string FindRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "tests", "golden", "cases.json"))
                    && File.Exists(Path.Combine(dir.FullName, "data", "units.json")))
                {
                    return dir.FullName;
                }
            }
            throw new InvalidOperationException("repository root (tests/golden/cases.json) not found above " + AppContext.BaseDirectory);
        }

        internal static string PathOf(params string[] parts) => Path.Combine(Root, Path.Combine(parts));

        /// <summary>"U+0631 U+064A …", readable in test output whatever the console encoding.</summary>
        internal static string CodePoints(string? s)
        {
            if (s == null) return "(null)";
            var parts = new string[s.Length];
            for (int i = 0; i < s.Length; i++) parts[i] = "U+" + ((int)s[i]).ToString("X4");
            return string.Join(" ", parts);
        }
    }
}
