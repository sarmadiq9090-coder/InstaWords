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
    /// Builds the report text, the mailto: link to FastKiwi support and the prefilled GitHub issue-form URL
    /// (.github/ISSUE_TEMPLATE/bug.yml: description, steps, environment; feature.yml: description, use_case).
    /// Nothing is ever sent automatically: the links open in the user's mail app or browser, where they send it themselves.
    /// </summary>
    public static class ReportBuilder
    {
        public const string NewIssueBase = "https://github.com/" + UpdateLogic.Owner + "/" + UpdateLogic.Repo + "/issues/new";
        public const int MaxUrlLength = 7000;
        /// <summary>Appended to a field shortened to fit the URL (in the interface language).</summary>
        public static string TruncatedNote => "\n\n… (" + Loc.T("Report.Truncated") + ")";

        public static string DefaultTitle(bool isBug) => Loc.T(isBug ? "Report.DefaultBugTitle" : "Report.DefaultIdeaTitle");

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
            sb.Append("[").Append(Loc.T(r.IsBug ? "Report.Bug" : "Report.Idea")).Append("] ").Append(EffectiveTitle(r)).Append("\n\n");
            sb.Append("## ").Append(Loc.T(r.IsBug ? "Report.BugDescription" : "Report.IdeaDescription")).Append("\n");
            sb.Append(string.IsNullOrWhiteSpace(r.Description) ? "(" + Loc.T("Report.NoDescription") + ")" : r.Description.Trim()).Append("\n");
            if (!string.IsNullOrWhiteSpace(r.Details))
            {
                sb.Append("\n## ").Append(Loc.T(r.IsBug ? "Report.StepsHeading" : "Report.WhyHeading")).Append("\n");
                sb.Append(r.Details.Trim()).Append("\n");
            }
            var env = EnvironmentText(r);
            if (env.Length > 0) sb.Append("\n## ").Append(Loc.T("Report.TechHeading")).Append("\n").Append(env).Append("\n");
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

        /// <summary>
        /// A mailto: link to <paramref name="email"/> with subject = the report title and body = <see cref="BuildText"/>,
        /// at most <paramref name="maxLength"/> characters: a long body is cut with <see cref="TruncatedNote"/>
        /// (the window then copies the full report for the user to paste). Null when the address is not usable.
        /// </summary>
        public static MailtoLink BuildMailto(ReportInput r, string email, int maxLength = SupportContact.MaxMailtoLength)
        {
            var to = SupportContact.EmailAddress(email);
            if (to == null) return null;
            var subject = EffectiveTitle(r);
            var body = Normalize(BuildText(r));
            var url = Mailto(to, subject, body);
            if (url.Length <= maxLength) return new MailtoLink { Url = url, Truncated = false };

            // Longest prefix of the body (plus the note) that fits; binary search on the number of characters kept.
            var note = Normalize(TruncatedNote);
            string Cut(string text, int keep) => text.Substring(0, CutLength(text, keep)).TrimEnd() + note;
            int lo = 0, hi = body.Length;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (Mailto(to, subject, Cut(body, mid)).Length <= maxLength) lo = mid; else hi = mid - 1;
            }
            url = Mailto(to, subject, Cut(body, lo));
            if (url.Length > maxLength)
            {
                // Even the note alone does not fit: shorten the subject too (and drop the note if it is still too long).
                var tail = note.TrimStart();
                if (Mailto(to, "", tail).Length > maxLength) tail = "";
                int slo = 0, shi = subject.Length;
                while (slo < shi)
                {
                    var mid = (slo + shi + 1) / 2;
                    if (Mailto(to, subject.Substring(0, CutLength(subject, mid)), tail).Length <= maxLength) slo = mid; else shi = mid - 1;
                }
                url = Mailto(to, subject.Substring(0, CutLength(subject, slo)), tail);
            }
            return new MailtoLink { Url = url, Truncated = true };
        }

        private static int CutLength(string text, int keep) =>
            keep > 0 && keep < text.Length && char.IsHighSurrogate(text[keep - 1]) ? keep - 1 : keep;

        /// <summary>RFC 6068: every character but unreserved ones percent-encoded (UTF-8), spaces as %20, line breaks as %0D%0A.</summary>
        public static string EncodeMailtoValue(string value) =>
            Uri.EscapeDataString((value ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n"));

        private static string Mailto(string to, string subject, string body) =>
            "mailto:" + to + "?subject=" + EncodeMailtoValue(subject) + "&body=" + EncodeMailtoValue(body);

        private static string Normalize(string text) => (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n");

        private static string Compose(IEnumerable<KeyValuePair<string, string>> fields) =>
            NewIssueBase + "?" + string.Join("&", fields
                .Where(f => !string.IsNullOrEmpty(f.Value))
                .Select(f => f.Key + "=" + Uri.EscapeDataString(f.Value)));
    }
}
