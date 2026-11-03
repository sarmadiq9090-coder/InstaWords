using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using InstaWords.Core.Internal;
using Xunit;

namespace InstaWords.Core.Tests
{
    /// <summary>The public surface the Windows app codes against.</summary>
    public class ApiTests
    {
        private static readonly JsonDocument UnitsJson = JsonDocument.Parse(File.ReadAllText(Repo.PathOf("data", "units.json")));

        [Fact]
        public void Options_defaults()
        {
            var o = new TafqitOptions();
            Assert.Equal("none", o.Unit);
            Assert.Equal("ar", o.Language);
            Assert.Equal("مائة", o.HundredsForm);
            Assert.Equal("", o.Prefix);
            Assert.Equal("", o.Suffix);
            Assert.Equal("round", o.Decimals);
        }

        [Fact]
        public void Convert_basics()
        {
            Assert.Equal("ألف ومائتان وخمسون ريالاً سعودياً وخمس وسبعون هللة", Tafqit.Convert("1250.75", new TafqitOptions { Unit = "SAR" }));
            Assert.Equal("One Thousand Two Hundred Fifty Saudi Riyals and Seventy-Five Halalas",
                Tafqit.Convert("1250.75", new TafqitOptions { Unit = "sar", Language = "en" }));
            Assert.Equal("mille deux cent cinquante riyals saoudiens et soixante-quinze halalas",
                Tafqit.Convert("1,250.75", new TafqitOptions { Unit = "ريال سعودي", Language = "fr" }));
            Assert.Equal("فقط مئتان وخمسة دراهم إماراتية لا غير",
                Tafqit.Convert(" ٢٠٥ ", new TafqitOptions { Unit = "AED", HundredsForm = "مئة", Prefix = " فقط ", Suffix = "لا غير " }));
            Assert.Equal("اثنا عشر فاصلة صفر خمسة", Tafqit.Convert("12.05"));
            Assert.Equal(Tafqit.Convert("12.05"), Tafqit.Convert("12.05", null));
        }

        [Fact]
        public void Null_options_mean_defaults_and_empty_strings_mean_defaults_except_decimals()
        {
            var nulls = new TafqitOptions { Unit = null, Language = null!, HundredsForm = null!, Prefix = null!, Suffix = null!, Decimals = null! };
            Assert.Equal(Tafqit.Convert("1200.5"), Tafqit.Convert("1200.5", nulls));
            var empties = new TafqitOptions { Unit = "", Language = "", HundredsForm = "", Prefix = "", Suffix = "" };
            Assert.Equal(Tafqit.Convert("1200.5"), Tafqit.Convert("1200.5", empties));
            // As in the core: decimals "" is not "round".
            var e = Assert.Throws<TafqitException>(() => Tafqit.Convert("1", new TafqitOptions { Decimals = "" }));
            Assert.Equal(TafqitErrorCodes.InvalidOption, e.Code);
            Assert.Equal("decimals=", e.Detail);
        }

        [Fact]
        public void Errors_throw_with_code_message_and_detail()
        {
            var e = Assert.Throws<TafqitException>(() => Tafqit.Convert("12a"));
            Assert.Equal("INVALID_FORMAT", e.Code);
            Assert.Equal("صيغة الرقم غير صحيحة.", e.Message);
            Assert.Null(e.Detail);

            e = Assert.Throws<TafqitException>(() => Tafqit.Convert("5", new TafqitOptions { Unit = "nope" }));
            Assert.Equal(TafqitErrorCodes.UnknownUnit, e.Code);
            Assert.Equal("الوحدة غير معروفة.", e.Message);
            Assert.Equal("nope", e.Detail);

            e = Assert.Throws<TafqitException>(() => Tafqit.Convert("5", new TafqitOptions { Language = "de" }));
            Assert.Equal(TafqitErrorCodes.InvalidOption, e.Code);
            Assert.Equal("language=de", e.Detail);

            e = Assert.Throws<TafqitException>(() => Tafqit.Convert("1.555", new TafqitOptions { Unit = "SAR", Decimals = "error" }));
            Assert.Equal(TafqitErrorCodes.TooManyDecimals, e.Code);
            Assert.Equal("ريال سعودي: 2", e.Detail);

            Assert.Equal(TafqitErrorCodes.Empty, Assert.Throws<TafqitException>(() => Tafqit.Convert(null!)).Code);
            Assert.Equal(TafqitErrorCodes.Negative, Assert.Throws<TafqitException>(() => Tafqit.Convert("-5")).Code);
            Assert.Equal(TafqitErrorCodes.TooLarge, Assert.Throws<TafqitException>(() => Tafqit.Convert("1000000000000000")).Code);
            Assert.Equal(TafqitErrorCodes.FractionNotSupported,
                Assert.Throws<TafqitException>(() => Tafqit.Convert("1.5", new TafqitOptions { Unit = "piece" })).Code);
        }

        [Fact]
        public void TryConvert_never_throws_and_keeps_cleaned()
        {
            var ok = Tafqit.TryConvert("٠٠١٬٢٣٤٫٥٠", new TafqitOptions { Unit = "SAR" });
            Assert.True(ok.Ok);
            Assert.Equal("1234.5", ok.Cleaned);
            Assert.Null(ok.ErrorCode);
            Assert.Null(ok.ErrorMessage);

            var bad = Tafqit.TryConvert("2,500", new TafqitOptions { Unit = "nope" });
            Assert.False(bad.Ok);
            Assert.Equal("", bad.Text);
            Assert.Equal("2500", bad.Cleaned); // the input itself was valid
            Assert.Equal(TafqitErrorCodes.UnknownUnit, bad.ErrorCode);
            Assert.Equal("الوحدة غير معروفة.", bad.ErrorMessage); // without detail

            var empty = Tafqit.TryConvert(null!);
            Assert.False(empty.Ok);
            Assert.Null(empty.Cleaned);
            Assert.Equal(TafqitErrorCodes.Empty, empty.ErrorCode);
            Assert.Equal("الرجاء إدخال رقم.", empty.ErrorMessage);
        }

        [Fact]
        public void Clean_matches_normalize()
        {
            Assert.Equal("1234.5", Tafqit.Clean("٠٠١٬٢٣٤٫٥٠"));
            Assert.Equal("0", Tafqit.Clean("+000"));
            Assert.Equal("0.5", Tafqit.Clean(".50"));
            Assert.Equal(TafqitErrorCodes.Empty, Assert.Throws<TafqitException>(() => Tafqit.Clean("  ")).Code);
            Assert.Equal(TafqitErrorCodes.InvalidFormat, Assert.Throws<TafqitException>(() => Tafqit.Clean("１")).Code);
        }

        [Fact]
        public void Error_codes_match_src_errors_ts()
        {
            var publicCodes = typeof(TafqitErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToArray();
            Assert.Equal(ErrorMessages.Codes, publicCodes);
            Assert.Equal(10, publicCodes.Length);
            foreach (var code in publicCodes) Assert.False(string.IsNullOrWhiteSpace(ErrorMessages.Get(code)));
        }

        [Fact]
        public void Units_list_the_catalog_in_file_order()
        {
            var json = UnitsJson.RootElement.GetProperty("units").EnumerateArray().ToArray();
            Assert.Equal(json.Length, Tafqit.Units.Count);
            Assert.Equal(106, Tafqit.Units.Count);
            for (int i = 0; i < json.Length; i++)
            {
                var j = json[i];
                var u = Tafqit.Units[i];
                Assert.Equal(j.GetProperty("id").GetString(), u.Id);
                Assert.Equal(j.GetProperty("category").GetString(), u.Category);
                Assert.Equal(j.GetProperty("label").GetString(), u.Label);
                Assert.Equal(j.TryGetProperty("code", out var code) ? code.GetString() : null, u.Code);
                var aliases = j.TryGetProperty("aliases", out var a) ? a.EnumerateArray().Select(x => x.GetString()!).ToArray() : new string[0];
                Assert.Equal(aliases, u.Aliases);
                Assert.Equal(j.TryGetProperty("subunit", out _), u.HasSubunit);
                bool hasI18n = j.TryGetProperty("i18n", out var i18n);
                Assert.Equal(hasI18n ? i18n.GetProperty("en").GetProperty("singular").GetString() : null, u.EnglishName);
                Assert.Equal(hasI18n ? i18n.GetProperty("fr").GetProperty("singular").GetString() : null, u.FrenchName);
                Assert.Same(u, Tafqit.FindUnit(u.Id));
            }
            Assert.Equal("none", Tafqit.Units[0].Id);
            Assert.Throws<NotSupportedException>(() => ((IList<UnitInfo>)Tafqit.Units)[0] = Tafqit.Units[1]);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)Tafqit.Units[1].Aliases)[0] = "x");
        }

        [Fact]
        public void FindUnit_by_any_name_like_the_core()
        {
            var sar = Tafqit.FindUnit("SAR")!;
            Assert.Equal("SAR", sar.Id);
            Assert.Equal("currency-arab", sar.Category);
            Assert.Equal("ريال سعودي", sar.Label);
            Assert.True(sar.HasSubunit);
            Assert.Equal("Saudi Riyal", sar.EnglishName);
            Assert.Equal("riyal saoudien", sar.FrenchName);
            Assert.Same(sar, Tafqit.FindUnit("  sAr "));
            Assert.Same(sar, Tafqit.FindUnit("ريال \t سعودي"));
            Assert.Same(Tafqit.FindUnit("kg"), Tafqit.FindUnit("KG")); // Kelvin sign lower-cases to k in JavaScript
            Assert.Equal("none", Tafqit.FindUnit("بدون")!.Id);
            Assert.False(Tafqit.FindUnit("none")!.HasSubunit);
            Assert.Null(Tafqit.FindUnit("none")!.EnglishName);
            Assert.Null(Tafqit.FindUnit(""));
            Assert.Null(Tafqit.FindUnit(" "));
            Assert.Null(Tafqit.FindUnit(null!));
            Assert.Null(Tafqit.FindUnit("nope"));
            Assert.Null(Tafqit.FindUnit("ＳＡＲ")); // full-width letters are not folded
        }

        [Fact]
        public void Languages()
        {
            Assert.Equal(new[] { "ar", "en", "fr" }, Tafqit.Languages.Select(l => l.Id));
            Assert.Equal(new[] { "العربية", "English", "Français" }, Tafqit.Languages.Select(l => l.Label));
            Assert.Equal(new[] { true, false, false }, Tafqit.Languages.Select(l => l.RightToLeft));
            Assert.Equal(new[] { true, false, false }, Tafqit.Languages.Select(l => l.SupportsHundredsForm));
        }

        [Fact]
        public void Category_labels_cover_every_category_in_order()
        {
            var expected = new Dictionary<string, string>
            {
                ["currency-arab"] = "عملات عربية", ["currency-world"] = "عملات عالمية", ["crypto"] = "عملات رقمية",
                ["generic-currency"] = "عملات بلا تحديد بلد", ["weight"] = "الأوزان", ["volume"] = "الأحجام والسعات", ["length"] = "الأطوال",
                ["area"] = "المساحات", ["time"] = "الزمن", ["count"] = "المعدودات", ["data"] = "سعة البيانات", ["speed"] = "السرعة",
                ["none"] = "بدون وحدة",
            };
            Assert.Equal(expected.ToArray(), Tafqit.CategoryLabels.ToArray());
            // Same set (and order) as the category enum of data/units.schema.json.
            using var schema = JsonDocument.Parse(File.ReadAllText(Repo.PathOf("data", "units.schema.json")));
            var enumValues = FindCategoryEnum(schema.RootElement)!.Value.EnumerateArray().Select(e => e.GetString()!).ToArray();
            Assert.Equal(enumValues, Tafqit.CategoryLabels.Keys.ToArray());
            foreach (var u in Tafqit.Units) Assert.True(Tafqit.CategoryLabels.ContainsKey(u.Category), u.Category);
        }

        private static JsonElement? FindCategoryEnum(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in e.EnumerateObject())
                {
                    if (p.Name == "category" && p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("enum", out var en)) return en;
                    var found = FindCategoryEnum(p.Value);
                    if (found != null) return found;
                }
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in e.EnumerateArray())
                {
                    var found = FindCategoryEnum(x);
                    if (found != null) return found;
                }
            }
            return null;
        }

        [Fact]
        public void Library_targets_netstandard20_without_dependencies()
        {
            var asm = typeof(Tafqit).Assembly;
            var tfm = asm.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()!.FrameworkName;
            Assert.Equal(".NETStandard,Version=v2.0", tfm);
            Assert.Equal(new[] { "netstandard" }, asm.GetReferencedAssemblies().Select(a => a.Name).ToArray());
        }

        [Fact]
        public void Public_surface_is_exactly_the_documented_one()
        {
            var types = typeof(Tafqit).Assembly.GetExportedTypes().Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[]
            {
                "InstaWords.Core.LanguageInfo", "InstaWords.Core.Tafqit", "InstaWords.Core.TafqitErrorCodes", "InstaWords.Core.TafqitException",
                "InstaWords.Core.TafqitOptions", "InstaWords.Core.TafqitResult", "InstaWords.Core.UnitInfo",
            }, types);
            Assert.All(typeof(TafqitResult).GetProperties(), p => Assert.Null(p.SetMethod));
            Assert.All(typeof(UnitInfo).GetProperties(), p => Assert.Null(p.SetMethod));
            Assert.All(typeof(LanguageInfo).GetProperties(), p => Assert.Null(p.SetMethod));
        }

        [Fact]
        public void Thread_safe()
        {
            string want = Tafqit.Convert("987654321012.345", new TafqitOptions { Unit = "KWD", Language = "fr" });
            var results = new string[2000];
            Parallel.For(0, results.Length, i => results[i] = Tafqit.Convert("987654321012.345", new TafqitOptions { Unit = "KWD", Language = "fr" }));
            Assert.All(results, r => Assert.Equal(want, r));
        }

        [Fact]
        public void Rounding_is_on_strings()
        {
            Assert.Equal("999999999999999.99", Tafqit.Clean("999999999999999.99"));
            Assert.Equal(TafqitErrorCodes.TooLarge, Tafqit.TryConvert("999999999999999.995", new TafqitOptions { Unit = "SAR" }).ErrorCode);
            Assert.Equal(("1000", "00"), Engine.RoundHalfUp("999", "995", 2));
            Assert.Equal(("0", "01"), Engine.RoundHalfUp("0", "005", 2));
            Assert.Equal(("0", "00"), Engine.RoundHalfUp("0", "0049", 2));
        }
    }
}
