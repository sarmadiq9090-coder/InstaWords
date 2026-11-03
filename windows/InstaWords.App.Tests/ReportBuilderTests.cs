using System;
using System.Collections.Generic;
using System.Linq;
using InstaWords.App.Services;
using Xunit;

namespace InstaWords.App.Tests
{
    public class ReportBuilderTests
    {
        private static ReportInput Bug(string description = "النتيجة خاطئة", string steps = "1. اكتب 100") => new ReportInput
        {
            IsBug = true,
            Title = "خطأ في التفقيط",
            Description = description,
            Details = steps,
            TechnicalInfo = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("إصدار البرنامج", "InstaWords 2.0.0") },
        };

        private static Dictionary<string, string> Query(string url)
        {
            var q = new Uri(url).Query.TrimStart('?');
            return q.Split('&').Select(p => p.Split(new[] { '=' }, 2))
                .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        }

        [Fact]
        public void BugUrlUsesBugFormFields()
        {
            var url = ReportBuilder.BuildIssueUrl(Bug());
            Assert.StartsWith("https://github.com/sarmadiq9090-coder/tafket/issues/new?", url);
            var q = Query(url);
            Assert.Equal("bug.yml", q["template"]);
            Assert.Equal("bug", q["labels"]);
            Assert.Equal("خطأ في التفقيط", q["title"]);
            Assert.Equal("النتيجة خاطئة", q["description"]);
            Assert.Equal("1. اكتب 100", q["steps"]);
            Assert.Equal("- إصدار البرنامج: InstaWords 2.0.0", q["environment"]);
            Assert.False(q.ContainsKey("use_case"));
        }

        [Fact]
        public void FeatureUrlUsesFeatureFormFields()
        {
            var r = Bug();
            r.IsBug = false;
            r.Details = "للشيكات";
            var q = Query(ReportBuilder.BuildIssueUrl(r));
            Assert.Equal("feature.yml", q["template"]);
            Assert.Equal("enhancement", q["labels"]);
            Assert.Equal("للشيكات", q["use_case"]);
            Assert.False(q.ContainsKey("steps"));
            Assert.False(q.ContainsKey("environment"));
        }

        [Fact]
        public void TechnicalInfoCanBeLeftOut()
        {
            var r = Bug();
            r.IncludeTechnicalInfo = false;
            Assert.False(Query(ReportBuilder.BuildIssueUrl(r)).ContainsKey("environment"));
            Assert.DoesNotContain("معلومات تقنية", ReportBuilder.BuildText(r));
            r.IncludeTechnicalInfo = true;
            Assert.Contains("InstaWords 2.0.0", ReportBuilder.BuildText(r));
        }

        [Fact]
        public void EmptyTitleGetsADefault()
        {
            var r = Bug();
            r.Title = "  ";
            Assert.Equal(ReportBuilder.DefaultTitle(true), Query(ReportBuilder.BuildIssueUrl(r))["title"]);
        }

        [Fact]
        public void LongReportsAreShortenedToFit()
        {
            var r = Bug(description: string.Concat(Enumerable.Repeat("وصف طويل جداً بالعربية ", 800)), steps: string.Concat(Enumerable.Repeat("خطوة ", 400)));
            var url = ReportBuilder.BuildIssueUrl(r);
            Assert.True(url.Length <= ReportBuilder.MaxUrlLength, "length " + url.Length);
            var q = Query(url);
            Assert.EndsWith(ReportBuilder.TruncatedNote, q["description"]);
            Assert.Equal("bug.yml", q["template"]);
            // The full text is still available for «نسخ التقرير».
            Assert.Contains(r.Description.Trim(), ReportBuilder.BuildText(r));
        }

        [Fact]
        public void ShortReportsAreNotTouched()
        {
            var url = ReportBuilder.BuildIssueUrl(Bug());
            Assert.DoesNotContain(Uri.EscapeDataString(ReportBuilder.TruncatedNote.Trim()), url);
        }
    }
}
