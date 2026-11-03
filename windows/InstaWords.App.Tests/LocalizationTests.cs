using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using InstaWords.App.Services;
using InstaWords.Core;
using Xunit;

namespace InstaWords.App.Tests
{
    /// <summary>The Arabic and English interface strings, and the language choice.</summary>
    public class LocalizationTests
    {
        private static Dictionary<string, string> Ar => TestStrings.Table("ar");
        private static Dictionary<string, string> En => TestStrings.Table("en");

        private static readonly Regex ArabicLetter = new Regex("[ء-يٱ-ۓﭐ-﷿ﹰ-ﻼ]");

        /// <summary>English strings that show Arabic on purpose.</summary>
        private static readonly HashSet<string> BilingualKeys = new HashSet<string>
        {
            "Settings.UiLanguage", // «Interface language · لغة الواجهة»: findable whatever the current language
        };

        [Fact]
        public void BothLanguagesHaveTheSameKeys()
        {
            var onlyAr = Ar.Keys.Except(En.Keys).OrderBy(k => k).ToList();
            var onlyEn = En.Keys.Except(Ar.Keys).OrderBy(k => k).ToList();
            Assert.True(onlyAr.Count == 0, "missing in Strings.en.xaml: " + string.Join(", ", onlyAr));
            Assert.True(onlyEn.Count == 0, "missing in Strings.ar.xaml: " + string.Join(", ", onlyEn));
            Assert.True(Ar.Count > 300, "only " + Ar.Count + " keys");
        }

        [Fact]
        public void EntriesHaveTheSameTypeInBothLanguages()
        {
            var ar = TestStrings.Kinds("ar");
            var en = TestStrings.Kinds("en");
            foreach (var kv in ar) Assert.Equal(kv.Value, en[kv.Key]);
            Assert.Equal("RightToLeft", Ar["UiFlow"]);
            Assert.Equal("LeftToRight", En["UiFlow"]);
        }

        [Fact]
        public void NoValueIsEmpty()
        {
            foreach (var table in new[] { Ar, En })
                foreach (var kv in table)
                    Assert.False(string.IsNullOrWhiteSpace(kv.Value), kv.Key + " is empty");
        }

        [Fact]
        public void PlaceholdersMatch()
        {
            var placeholder = new Regex(@"\{(\d+)\}");
            foreach (var key in Ar.Keys)
            {
                var a = placeholder.Matches(Ar[key]).Select(m => m.Value).Distinct().OrderBy(s => s).ToList();
                var e = placeholder.Matches(En[key]).Select(m => m.Value).Distinct().OrderBy(s => s).ToList();
                Assert.True(a.SequenceEqual(e), key + ": ar " + string.Join(",", a) + " / en " + string.Join(",", e));
                // Every placeholder up to the highest one is used (Loc.F would otherwise silently drop an argument).
                for (var i = 0; i < a.Count; i++) Assert.Contains("{" + i + "}", a);
            }
        }

        [Fact]
        public void EnglishIsEnglish()
        {
            foreach (var kv in En)
            {
                if (BilingualKeys.Contains(kv.Key)) continue;
                Assert.False(ArabicLetter.IsMatch(kv.Value), kv.Key + " has Arabic letters: " + kv.Value);
            }
        }

        [Fact]
        public void ArabicIsTranslated()
        {
            // Strings that are the same in both languages are names or codes, never untranslated sentences.
            var same = Ar.Keys.Where(k => k != "UiFlow" && Ar[k] == En[k] && Ar[k].Contains(' ')).ToList();
            Assert.True(same.Count == 0, "identical in ar and en: " + string.Join(", ", same));
        }

        /// <summary>Every key the app's XAML and code ask for exists (a missing one would show its key).</summary>
        [Fact]
        public void EveryKeyTheAppUsesExists()
        {
            var used = UsedKeys();
            Assert.True(used.Count > 300, "found only " + used.Count + " keys in the sources");
            var missing = used.Where(k => !Ar.ContainsKey(k)).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0, "used but not defined: " + string.Join(", ", missing));
        }

        /// <summary>No dead entries: every string is used somewhere.</summary>
        [Fact]
        public void EveryKeyIsUsed()
        {
            var used = UsedKeys();
            var unused = Ar.Keys.Where(k => !used.Contains(k)).OrderBy(k => k).ToList();
            Assert.True(unused.Count == 0, "defined but never used: " + string.Join(", ", unused));
        }

        private static HashSet<string> UsedKeys()
        {
            var used = new HashSet<string>(StringComparer.Ordinal) { "UiFlow" };
            var app = TestStrings.AppDir;
            var files = Directory.EnumerateFiles(app, "*.*", SearchOption.AllDirectories)
                .Where(f => (f.EndsWith(".xaml") || f.EndsWith(".cs")) && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                            && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                            && !f.Contains(Path.DirectorySeparatorChar + "Strings" + Path.DirectorySeparatorChar));
            var dynamicResource = new Regex(@"DynamicResource\s+([A-Za-z][\w.\-]*)");
            // String literals that look like keys: "Group.Name" with a known group.
            var groups = Ar.Keys.Select(k => k.Split('.')[0]).Distinct().ToList();
            // (A literal ending with "." is a prefix completed at run time; those are listed below.)
            var literal = new Regex("\"((?:" + string.Join("|", groups.Select(Regex.Escape)) + @")\.[A-Za-z][\w.\-]*[\w\-])""");
            foreach (var f in files)
            {
                var text = File.ReadAllText(f);
                if (f.EndsWith(".xaml"))
                {
                    text = Regex.Replace(text, "<!--.*?-->", "", RegexOptions.Singleline);
                    foreach (Match m in dynamicResource.Matches(text)) used.Add(m.Groups[1].Value);
                }
                else foreach (Match m in literal.Matches(text)) used.Add(m.Groups[1].Value);
            }
            // Keys built at run time.
            foreach (var code in typeof(TafqitErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(fi => (string)fi.GetValue(null)))
                used.Add("Err." + code);
            foreach (var cat in Tafqit.CategoryLabels.Keys) used.Add("Cat." + cat);
            foreach (var t in PrintTemplates.All)
            {
                used.Add(PrintTemplates.NameKey(t));
                used.Add("Print.Hint." + t);
            }
            foreach (var f in PrintTemplates.Calibrated) used.Add("Print.Field." + f);
            return used;
        }

        [Fact]
        public void TemplateFieldAndPaperKeysExist()
        {
            foreach (var t in PrintTemplates.All)
            {
                Assert.True(Ar.ContainsKey(PrintTemplates.NameKey(t)), t.ToString());
                Assert.True(Ar.ContainsKey(PrintTemplates.PartyKey(t)), t.ToString());
                foreach (var p in Papers.For(t)) Assert.True(Ar.ContainsKey(p.LabelKey), p.LabelKey);
                foreach (var k in PrintTemplates.SignatureKeys(t)) Assert.True(Ar.ContainsKey(k), k);
                var title = PrintTemplates.TitleKey(t);
                if (title != null) Assert.True(Ar.ContainsKey(title), title);
            }
        }

        /// <summary>The Arabic error texts are the engine's own (the app shows the engine's message in Arabic).</summary>
        [Theory]
        [InlineData("", "SAR", "ar", "round")]
        [InlineData("12abc", "SAR", "ar", "round")]
        [InlineData("-5", "SAR", "ar", "round")]
        [InlineData("1000000000000000", "SAR", "ar", "round")]
        [InlineData("1.125", "SAR", "ar", "error")]
        [InlineData("5", "no-such-unit", "ar", "round")]
        [InlineData("5", "SAR", "de", "round")]
        public void ArabicErrorsAreTheEngineMessages(string input, string unit, string language, string decimals)
        {
            var r = Tafqit.TryConvert(input, new TafqitOptions { Unit = unit, Language = language, Decimals = decimals });
            Assert.False(r.Ok);
            Assert.Equal(r.ErrorMessage, Ar["Err." + r.ErrorCode]);
            Assert.Equal(r.ErrorMessage, Loc.Error(r.ErrorCode, r.ErrorMessage));
            TestStrings.With("en", () =>
            {
                var en = Loc.Error(r.ErrorCode, r.ErrorMessage);
                Assert.Equal(En["Err." + r.ErrorCode], en);
                Assert.False(ArabicLetter.IsMatch(en), en);
            });
        }

        [Fact]
        public void UnknownErrorCodesFallBack()
        {
            TestStrings.With("en", () =>
            {
                Assert.Equal("مشكلة", Loc.Error("SOMETHING_NEW", "مشكلة"));
                Assert.Equal(En["Err.Generic"], Loc.Error("SOMETHING_NEW", null));
            });
        }

        [Theory]
        [InlineData("auto", "ar-SA", "ar")]
        [InlineData("auto", "ar", "ar")]
        [InlineData("auto", "AR-iq", "ar")]
        [InlineData("auto", "en-US", "en")]
        [InlineData("auto", "fr-FR", "en")]
        [InlineData("auto", "", "en")]
        [InlineData(null, "ar-EG", "ar")]
        [InlineData("ar", "en-US", "ar")]
        [InlineData("en", "ar-SA", "en")]
        [InlineData("arx", "ar-SA", "ar")]
        public void ResolvesTheInterfaceLanguage(string setting, string windows, string expected) =>
            Assert.Equal(expected, Loc.Resolve(Loc.NormalizeSetting(setting), windows));

        [Theory]
        [InlineData("arabic", "ar")]
        [InlineData("English", "en")]
        [InlineData("french", null)]
        [InlineData(null, null)]
        public void ReadsTheInstallerLanguage(string inno, string expected) => Assert.Equal(expected, Loc.FromInstallerLanguage(inno));

        [Fact]
        public void FormatsAndFallsBack()
        {
            Assert.Equal("Missing.Key", Loc.T("Missing.Key"));
            TestStrings.With("en", () =>
            {
                Assert.True(Loc.IsEnglish);
                Assert.False(Loc.IsRightToLeft);
                Assert.Equal("Update 2.1.0 available", Loc.F("Main.UpdateAvailableVersion", "2.1.0"));
                Assert.Equal("Pasted 1,234 numbers from the clipboard.", Loc.F("Bulk.Pasted", "1,234"));
            });
            Assert.Equal("تحديث متاح 2.1.0", Loc.F("Main.UpdateAvailableVersion", "2.1.0"));
        }

        [Fact]
        public void ChangedIsRaisedOnSwitch()
        {
            var raised = 0;
            EventHandler h = (s, e) => raised++;
            Loc.Changed += h;
            try
            {
                TestStrings.With("en", () => { });
            }
            finally
            {
                Loc.Changed -= h;
            }
            Assert.Equal(2, raised); // to English, and back
        }

        [Fact]
        public void ReportsFollowTheInterfaceLanguage()
        {
            var input = new ReportInput { IsBug = true, Title = "", Description = "Result is wrong", Details = "1. type 100" };
            Assert.Contains("## وصف المشكلة", ReportBuilder.BuildText(input));
            TestStrings.With("en", () =>
            {
                var text = ReportBuilder.BuildText(input);
                Assert.StartsWith("[Problem] Problem in InstaWords for Windows", text);
                Assert.Contains("## What went wrong", text);
                Assert.Contains("## Steps to reproduce", text);
                Assert.False(ArabicLetter.IsMatch(text), text);
            });
        }
    }
}
