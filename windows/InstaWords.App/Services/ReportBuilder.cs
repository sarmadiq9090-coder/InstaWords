using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Pure logic (no WPF): compiled into the app and linked into InstaWords.App.Tests.
namespace InstaWords.App.Services
{
    /// <summary>What the user typed in «الإبلاغ عن مشكلة أو اقتراح».</summary>
    public sealed class ReportInput
    {
        public bool IsBug { get; set; } = true;
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        /// <summary>Bug: steps to reproduce (form field <c>steps</c>). Suggestion: why it is needed (<c>use_case</c>).</summary>
        public string Details { get; set; } = "";
        public bool IncludeTechnicalInfo { get; set; } = true;
        /// <summary>Label → value, e.g. «إصدار البرنامج» → 2.0.0. Never amounts, clipboard or history.</summary>
        public IList<KeyValuePair<string, string>> TechnicalInfo { get; set; } = new List<KeyValuePair<string, string>>();
    }

    /// <summary>
    /// Builds the report text and the prefilled GitHub issue-form URL
    /// (.github/ISSUE_TEMPLATE/bug.yml: description, steps, environment; feature.yml: description, use_case).
    /// Nothing is ever sent automatically: the URL opens in the browser, where the user submits it.
    /// </summary>
    public static class ReportBuilder
    {
        public const string NewIssueBase = "https://github.com/" + UpdateLogic.Owner + "/" + UpdateLogic.Repo + "/issues/new";
        public const int MaxUrlLength = 7000;
        public const string TruncatedNote = "\n\n… (اختُصر النص ليتسع له الرابط. النص الكامل: «نسخ التقرير» في البرنامج.)";

        public static string DefaultTitle(bool isBug) => isBug ? "مشكلة في برنامج InstaWords لـ Windows" : "اقتراح لبرنامج InstaWords لـ Windows";

        public static string EffectiveTitle(ReportInput r) =>
            string.IsNullOrWhiteSpace(r.Title) ? DefaultTitle(r.IsBug) : r.Title.Trim();

        public static string EnvironmentText(ReportInput r)
        {
            if (!r.IncludeTechnicalInfo || r.TechnicalInfo == null || r.TechnicalInfo.Count == 0) return "";
            return string.Join("\n", r.TechnicalInfo.Select(kv => "- " + kv.Key + ": " + kv.Value));
        }

        /// <summary>The full report, exactly what the preview shows and «نسخ التقرير» copies.</summary>
        public static string BuildText(ReportInput r)
        {
            var sb = new StringBuilder();
            sb.Append(r.IsBug ? "[مشكلة] " : "[اقتراح] ").Append(EffectiveTitle(r)).Append("\n\n");
            sb.Append(r.IsBug ? "## وصف المشكلة\n" : "## الاقتراح\n");
            sb.Append(string.IsNullOrWhiteSpace(r.Description) ? "(لا يوجد وصف)" : r.Description.Trim()).Append("\n");
            if (!string.IsNullOrWhiteSpace(r.Details))
            {
                sb.Append("\n").Append(r.IsBug ? "## خطوات إعادة المشكلة\n" : "## لماذا تحتاجه؟\n");
                sb.Append(r.Details.Trim()).Append("\n");
            }
            var env = EnvironmentText(r);
            if (env.Length > 0) sb.Append("\n## معلومات تقنية\n").Append(env).Append("\n");
            return sb.ToString();
        }

        /// <summary>The issue-form URL, at most <paramref name="maxLength"/> characters (long fields are shortened with a note).</summary>
        public static string BuildIssueUrl(ReportInput r, int maxLength = MaxUrlLength)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("template", r.IsBug ? "bug.yml" : "feature.yml"),
                new KeyValuePair<string, string>("labels", r.IsBug ? "bug" : "enhancement"),
                new KeyValuePair<string, string>("title", EffectiveTitle(r)),
                new KeyValuePair<string, string>("description", (r.Description ?? "").Trim()),
            };
            if (r.IsBug)
            {
                fields.Add(new KeyValuePair<string, string>("steps", (r.Details ?? "").Trim()));
                fields.Add(new KeyValuePair<string, string>("environment", EnvironmentText(r)));
            }
            else fields.Add(new KeyValuePair<string, string>("use_case", (r.Details ?? "").Trim()));

            var url = Compose(fields);
            // Shorten the longest free-text field until the URL fits (Arabic grows ~6× when percent-encoded).
            var guard = 0;
            while (url.Length > maxLength && guard++ < 64)
            {
                var idx = -1;
                var longest = 0;
                for (var i = 2; i < fields.Count; i++)
                {
                    var len = Uri.EscapeDataString(fields[i].Value).Length;
                    if (len > longest) { longest = len; idx = i; }
                }
                if (idx < 0) break;
                var value = fields[idx].Value;
                if (value.EndsWith(TruncatedNote, StringComparison.Ordinal)) value = value.Substring(0, value.Length - TruncatedNote.Length);
                var over = url.Length - maxLength;
                // Remove enough characters, estimated from the field's encoded density, plus room for the note.
                var density = Math.Max(1.0, (double)longest / Math.Max(1, fields[idx].Value.Length));
                var cut = (int)Math.Ceiling((over + Uri.EscapeDataString(TruncatedNote).Length) / density) + 1;
                var keep = Math.Max(0, value.Length - cut);
                if (keep > 0 && char.IsHighSurrogate(value[keep - 1])) keep--;
                var shortened = idx == 2 ? value.Substring(0, Math.Min(value.Length, Math.Max(10, keep))) : value.Substring(0, keep).TrimEnd() + TruncatedNote;
                if (shortened == fields[idx].Value) break;
                fields[idx] = new KeyValuePair<string, string>(fields[idx].Key, shortened);
                url = Compose(fields);
            }
            return url;
        }

        private static string Compose(IEnumerable<KeyValuePair<string, string>> fields) =>
            NewIssueBase + "?" + string.Join("&", fields
                .Where(f => !string.IsNullOrEmpty(f.Value))
                .Select(f => f.Key + "=" + Uri.EscapeDataString(f.Value)));
    }
}
