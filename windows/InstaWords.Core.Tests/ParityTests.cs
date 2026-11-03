using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using InstaWords.Core.Internal;
using Xunit;

namespace InstaWords.Core.Tests
{
    /// <summary>
    /// The parity corpus (scripts/gen-dotnet-corpus.mjs): every conversion and every Clean must give
    /// exactly what the TypeScript core gives (dist/index.js), including the error code, the message
    /// without detail (TryConvert) and with detail (TafqitException), and the cleaned input.
    /// </summary>
    public class ParityTests
    {
        private static readonly string CorpusPath = Repo.PathOf("windows", "InstaWords.Core.Tests", "corpus", "parity.tsv");

        private sealed class Corpus
        {
            public string SourceHash = "";
            public readonly List<string?[]> Conversions = new List<string?[]>();
            public readonly List<string?[]> Cleans = new List<string?[]>();
        }

        private static readonly Lazy<Corpus> Data = new Lazy<Corpus>(Load);

        private static Corpus Load()
        {
            if (!File.Exists(CorpusPath))
            {
                throw new InvalidOperationException($"{CorpusPath} is missing: run \"npm run build && node scripts/gen-dotnet-corpus.mjs\" first.");
            }
            var corpus = new Corpus();
            foreach (var line in File.ReadLines(CorpusPath, Encoding.UTF8))
            {
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    if (line.StartsWith("# source-hash ", StringComparison.Ordinal)) corpus.SourceHash = line.Substring(14);
                    continue;
                }
                var fields = line.Split('\t').Select(Decode).ToArray();
                if (fields[0] == "T" && fields.Length == 13) corpus.Conversions.Add(fields);
                else if (fields[0] == "C" && fields.Length == 4) corpus.Cleans.Add(fields);
                else throw new InvalidDataException("bad corpus line: " + line);
            }
            return corpus;
        }

        /// <summary>"~" = absent; "\\" and \uXXXX escapes.</summary>
        private static string? Decode(string field)
        {
            if (field == "~") return null;
            if (field.IndexOf('\\') < 0) return field;
            var sb = new StringBuilder(field.Length);
            for (int i = 0; i < field.Length; i++)
            {
                char c = field[i];
                if (c != '\\') { sb.Append(c); continue; }
                char n = field[++i];
                if (n == '\\') sb.Append('\\');
                else if (n == 'u') { sb.Append((char)Convert.ToInt32(field.Substring(i + 1, 4), 16)); i += 4; }
                else throw new InvalidDataException("bad escape in corpus: " + field);
            }
            return sb.ToString();
        }

        /// <summary>Mirror of customCatalog() in scripts/gen-dotnet-corpus.mjs.</summary>
        internal static UnitData[] CustomCatalog()
        {
            var units = BuiltInUnits.Create();
            UnitData By(string id) => units.Single(u => u.Id == id);
            By("SAR").Subunit!.Fr = null;
            By("KWD").En = null;
            By("riyal").En = null;
            By("riyal").Fr = null;
            By("BTC").Subunit!.En = null;
            By("BTC").Subunit!.Fr = null;
            By("EUR").En!.One = "One Single Euro";
            By("EUR").Subunit!.Fr!.One = "un seul centime";
            By("lira").Fr!.Feminine = !By("lira").Fr!.Feminine;
            By("riyal").Forms!.One = "ريال فقط";
            return units;
        }

        private static readonly Engine DefaultEngine = new Engine(BuiltInUnits.Create());
        private static readonly Engine CustomEngine = new Engine(CustomCatalog());

        [Fact]
        public void Corpus_was_generated_from_the_same_sources_as_Generated()
        {
            Assert.True(Data.Value.SourceHash == GeneratedInfo.SourceHash,
                $"corpus source hash {Data.Value.SourceHash} != Generated {GeneratedInfo.SourceHash}: " +
                "run node scripts/build-dotnet.mjs, npm run build and node scripts/gen-dotnet-corpus.mjs");
        }

        [Fact]
        public void Corpus_is_large_and_covers_every_outcome()
        {
            var c = Data.Value.Conversions;
            Assert.True(c.Count > 100000, $"corpus too small: {c.Count}");
            Assert.True(c.Count(f => f[9] == "1") > 90000, "too few successful conversions");
            foreach (var lang in new[] { "ar", "en", "fr" }) Assert.True(c.Count(f => (f[8] ?? "ar") == lang) > 25000, $"few {lang} cases");
            foreach (var code in ErrorMessages.Codes)
            {
                if (code == TafqitErrorCodes.InvalidCatalog) continue; // the C# catalog is validated by the generator
                Assert.True(c.Count(f => f[9] == "0" && f[10] == code) >= 5, $"corpus has too few {code} cases");
            }
            Assert.True(c.Count(f => f[1] == "1") > 500, "too few custom-catalog cases");
            Assert.True(Data.Value.Cleans.Count > 20000, "too few Clean cases");
        }

        [Fact]
        public void Every_conversion_equals_the_core()
        {
            var failures = new List<string>();
            int n = 0;
            foreach (var f in Data.Value.Conversions)
            {
                n++;
                var engine = f[1] == "1" ? CustomEngine : DefaultEngine;
                string? input = f[2];
                var options = new TafqitOptions { Unit = f[3], HundredsForm = f[4]!, Prefix = f[5]!, Suffix = f[6]!, Decimals = f[7]!, Language = f[8]! };
                bool ok = f[9] == "1";
                string want = f[10]!;
                string? wantMessage = f[11];
                string? wantCleaned = f[12];

                string problem = Check(engine, input!, options, ok, want, wantMessage, wantCleaned);
                if (problem.Length > 0 && failures.Count < 20)
                {
                    failures.Add($"#{n} input=[{Repo.CodePoints(input)}] unit={f[3] ?? "~"} hf={f[4] ?? "~"} pre={f[5] ?? "~"} suf={f[6] ?? "~"} " +
                        $"dec={f[7] ?? "~"} lang={f[8] ?? "~"} custom={f[1]}: {problem}");
                }
                else if (problem.Length > 0) failures.Add("");
            }
            Assert.True(failures.Count == 0, $"{failures.Count} of {n} conversions differ from the core:\n" + string.Join("\n", failures.Where(s => s.Length > 0)));
        }

        private static string Check(Engine engine, string input, TafqitOptions options, bool ok, string want, string? wantMessage, string? wantCleaned)
        {
            var r = engine.TryConvert(input, options);
            if (r.Cleaned != wantCleaned) return $"cleaned [{r.Cleaned}] != [{wantCleaned}]";
            if (ok)
            {
                if (!r.Ok) return $"error {r.ErrorCode}, expected [{want}]";
                if (r.Text != want) return $"\n  expected: {want}\n  got:      {r.Text}\n  expected: {Repo.CodePoints(want)}\n  got:      {Repo.CodePoints(r.Text)}";
                if (r.ErrorCode != null || r.ErrorMessage != null) return "error fields set on success";
                string text = engine.Convert(input, options);
                if (text != want) return $"Convert [{text}] != TryConvert";
                return "";
            }
            if (r.Ok) return $"got [{r.Text}], expected error {want}";
            if (r.ErrorCode != want) return $"error {r.ErrorCode}, expected {want}";
            if (r.Text != "") return "Text not empty on error";
            if (wantMessage == null || !(wantMessage == r.ErrorMessage || wantMessage.StartsWith(r.ErrorMessage + " (", StringComparison.Ordinal)))
            {
                return $"message [{r.ErrorMessage}] is not the core's [{wantMessage}] without detail";
            }
            try
            {
                engine.Convert(input, options);
                return "Convert did not throw";
            }
            catch (TafqitException e)
            {
                string full = e.Detail == null ? e.Message : e.Message + " (" + e.Detail + ")";
                if (e.Code != want) return $"Convert threw {e.Code}";
                if (e.Message != r.ErrorMessage) return "exception Message != TryConvert ErrorMessage";
                if (full != wantMessage) return $"message with detail [{full}] != core [{wantMessage}]";
                return "";
            }
        }

        [Fact]
        public void Every_clean_equals_normalizeNumber()
        {
            var failures = new List<string>();
            foreach (var f in Data.Value.Cleans)
            {
                string input = f[1]!;
                string got;
                try
                {
                    got = "1:" + Tafqit.Clean(input);
                }
                catch (TafqitException e)
                {
                    got = "0:" + e.Code;
                }
                string want = f[2] + ":" + f[3];
                if (got != want && failures.Count < 20) failures.Add($"[{Repo.CodePoints(input)}]: {got} != {want}");
                else if (got != want) failures.Add("");
            }
            Assert.True(failures.Count == 0, $"{failures.Count} Clean results differ:\n" + string.Join("\n", failures.Where(s => s.Length > 0)));
        }
    }
}
