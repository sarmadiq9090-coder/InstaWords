using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace InstaWords.Core.Tests
{
    /// <summary>tests/golden/cases.json, as tests/golden.test.ts runs it.</summary>
    public class GoldenTests
    {
        private static readonly JsonDocument Golden = JsonDocument.Parse(File.ReadAllText(Repo.PathOf("tests", "golden", "cases.json")));

        public static IEnumerable<object[]> CaseIds()
        {
            foreach (var c in Golden.RootElement.GetProperty("cases").EnumerateArray()) yield return new object[] { c.GetProperty("id").GetString()! };
        }

        private static JsonElement Case(string id)
        {
            foreach (var c in Golden.RootElement.GetProperty("cases").EnumerateArray()) if (c.GetProperty("id").GetString() == id) return c;
            throw new KeyNotFoundException(id);
        }

        [Fact]
        public void HasAllCases() => Assert.Equal(107, Golden.RootElement.GetProperty("cases").GetArrayLength());

        [Theory]
        [MemberData(nameof(CaseIds))]
        public void Case_matches_expected(string id)
        {
            var c = Case(id);
            // { ...golden.defaults, ...c.options, unit: c.unit, language: c.options?.language }
            var merged = new Dictionary<string, string>();
            foreach (var p in Golden.RootElement.GetProperty("defaults").EnumerateObject()) merged[p.Name] = p.Value.GetString()!;
            if (c.TryGetProperty("options", out var o)) foreach (var p in o.EnumerateObject()) merged[p.Name] = p.Value.GetString()!;
            var options = new TafqitOptions
            {
                Unit = c.GetProperty("unit").GetString(),
                HundredsForm = merged.TryGetValue("hundredsForm", out var hf) ? hf : null!,
                Prefix = merged.TryGetValue("prefix", out var pre) ? pre : null!,
                Suffix = merged.TryGetValue("suffix", out var suf) ? suf : null!,
                Decimals = merged.TryGetValue("decimals", out var dec) ? dec : null!,
                Language = merged.TryGetValue("language", out var lang) ? lang : null!,
            };
            string input = c.GetProperty("input").GetString()!;
            string expected = c.GetProperty("expected").GetString()!;
            Assert.Equal(expected, Tafqit.Convert(input, options));
            Assert.Equal(c.GetProperty("cleaned").GetString(), Tafqit.Clean(input));
            var r = Tafqit.TryConvert(input, options);
            Assert.True(r.Ok);
            Assert.Equal(expected, r.Text);
            Assert.Equal(c.GetProperty("cleaned").GetString(), r.Cleaned);
        }
    }
}
