using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using InstaWords.App.Services;
using Xunit;

// Loc is global state: tests that switch the interface language must not run beside the others.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace InstaWords.App.Tests
{
    /// <summary>
    /// Reads Strings/Strings.{ar,en}.xaml the way XAML does (without WPF, so on any OS) and installs a
    /// language into <see cref="Loc"/> for the linked app logic.
    /// </summary>
    internal static class TestStrings
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly XNamespace Xml = XNamespace.Xml;
        private static readonly Dictionary<string, Dictionary<string, string>> Cache = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>The app's linked logic speaks Arabic by default, as the app does for an Arabic setting.</summary>
        [ModuleInitializer]
        internal static void Init() => Use(Loc.Arabic);

        public static string WindowsDir { get; } = FindWindowsDir();
        public static string AppDir => Path.Combine(WindowsDir, "InstaWords.App");
        public static string StringsFile(string language) => Path.Combine(AppDir, "Strings", "Strings." + language + ".xaml");

        public static void Use(string language) => Loc.Set(language, Table(language));

        /// <summary>Runs <paramref name="action"/> with the interface in <paramref name="language"/>, then back to Arabic.</summary>
        public static void With(string language, Action action)
        {
            Use(language);
            try { action(); }
            finally { Use(Loc.Arabic); }
        }

        public static Dictionary<string, string> Table(string language)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(language, out var t)) return t;
                return Cache[language] = Parse(StringsFile(language));
            }
        }

        /// <summary>All x:Key'd entries: sys:String values (XAML whitespace rules), and other entries as their text.</summary>
        public static Dictionary<string, string> Parse(string path)
        {
            var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in doc.Root.Elements())
            {
                var key = (string)e.Attribute(X + "Key");
                if (key == null) continue;
                if (table.ContainsKey(key)) throw new InvalidDataException("duplicate key " + key + " in " + path);
                var preserve = (string)e.Attribute(Xml + "space") == "preserve";
                table[key] = preserve ? e.Value : Regex.Replace(e.Value, @"\s+", " ").Trim();
            }
            return table;
        }

        /// <summary>Element names (sys:String, FlowDirection) by key.</summary>
        public static Dictionary<string, string> Kinds(string language) =>
            XDocument.Load(StringsFile(language)).Root.Elements()
                .Where(e => e.Attribute(X + "Key") != null)
                .ToDictionary(e => (string)e.Attribute(X + "Key"), e => e.Name.LocalName);

        private static string FindWindowsDir()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "InstaWords.sln"))) return d.FullName;
            throw new DirectoryNotFoundException("windows/InstaWords.sln not found above " + AppContext.BaseDirectory);
        }
    }
}
