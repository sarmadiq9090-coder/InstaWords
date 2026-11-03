using System;
using System.Collections.Generic;
using System.Linq;
using InstaWords.App.Services;
using Xunit;

namespace InstaWords.App.Tests
{
    /// <summary>«التواصل مع FastKiwi» / «إرسال بالبريد الإلكتروني»: the support contact and the mailto: link.</summary>
    public class SupportContactTests
    {
        private const string To = "support@fastkiwi.app";

        private static ReportInput Bug(string description = "النتيجة خاطئة", string steps = "1. اكتب 100\n2. اضغط نسخ") => new ReportInput
        {
            IsBug = true,
            Title = "خطأ في التفقيط & الحروف?",
            Description = description,
            Details = steps,
            TechnicalInfo = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("إصدار البرنامج", "InstaWords 2.0.0") },
        };

        /// <summary>The subject and body of a mailto: link, decoded.</summary>
        private static (string To, Dictionary<string, string> Fields) Parse(string url)
        {
            Assert.StartsWith("mailto:", url);
            var rest = url.Substring("mailto:".Length);
            var q = rest.IndexOf('?');
            var fields = rest.Substring(q + 1).Split('&').Select(p => p.Split(new[] { '=' }, 2))
                .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
            return (rest.Substring(0, q), fields);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("TODO")]
        [InlineData("todo: support email")]
        [InlineData("support@example.com")]
        [InlineData("<email>")]
        public void PlaceholdersAreDetected(string value)
        {
            Assert.True(SupportContact.IsPlaceholder(value));
            Assert.Null(SupportContact.EmailAddress(value));
            Assert.Null(SupportContact.TelegramUrl(value));
            Assert.Null(ReportBuilder.BuildMailto(Bug(), value));
        }

        [Theory]
        [InlineData("support@fastkiwi.app", "support@fastkiwi.app")]
        [InlineData("  support@fastkiwi.app ", "support@fastkiwi.app")]
        [InlineData("mailto:help.desk+iw@mail.fastkiwi.app", "help.desk+iw@mail.fastkiwi.app")]
        [InlineData("not an address", null)]
        [InlineData("support@fastkiwi", null)]
        [InlineData("a@b.app?cc=x@y.app", null)]
        [InlineData("a b@fastkiwi.app", null)]
        public void EmailAddressesAreValidated(string value, string expected) =>
            Assert.Equal(expected, SupportContact.EmailAddress(value));

        [Theory]
        [InlineData("fastkiwi", "https://t.me/fastkiwi")]
        [InlineData("@fastkiwi", "https://t.me/fastkiwi")]
        [InlineData("t.me/fastkiwi", "https://t.me/fastkiwi")]
        [InlineData("https://t.me/FastKiwi_Support/", "https://t.me/FastKiwi_Support")]
        [InlineData("http://t.me/fastkiwi", "https://t.me/fastkiwi")]
        [InlineData("https://evil.example/fastkiwi", null)]
        [InlineData("fk", null)]
        [InlineData("fast kiwi", null)]
        public void TelegramLinksAreNormalized(string value, string expected) =>
            Assert.Equal(expected, SupportContact.TelegramUrl(value));

        [Fact]
        public void ConstantsAreEmptyOrValid()
        {
            // Either still a placeholder (the support page is used) or a real, usable contact.
            Assert.True(SupportContact.IsPlaceholder(SupportContact.Email) || SupportContact.ConfiguredEmail != null, "SupportContact.Email is not a valid address");
            Assert.True(SupportContact.IsPlaceholder(SupportContact.Telegram) || SupportContact.ConfiguredTelegram != null, "SupportContact.Telegram is not a valid name or t.me link");
        }

        [Fact]
        public void SupportPageFollowsTheInterfaceLanguage()
        {
            Assert.Equal("https://fastkiwi.app/ar/support/", SupportContact.SupportPage(Loc.Arabic));
            Assert.Equal("https://fastkiwi.app/support/", SupportContact.SupportPage(Loc.English));
            Assert.Equal("https://fastkiwi.app/ar/support/", SupportContact.SupportPage(null));
        }

        [Fact]
        public void MailtoHasTheTitleAsSubjectAndTheReportAsBody()
        {
            var r = Bug();
            var mail = ReportBuilder.BuildMailto(r, To);
            Assert.False(mail.Truncated);
            var (to, fields) = Parse(mail.Url);
            Assert.Equal(To, to);
            Assert.Equal(new[] { "subject", "body" }, fields.Keys.ToArray());
            Assert.Equal(ReportBuilder.EffectiveTitle(r), fields["subject"]);
            Assert.Equal(ReportBuilder.BuildText(r).Replace("\n", "\r\n"), fields["body"]);
            Assert.Contains("InstaWords 2.0.0", fields["body"]);
        }

        [Fact]
        public void EmptyTitleGetsTheDefaultSubject()
        {
            var r = Bug();
            r.Title = " ";
            Assert.Equal(ReportBuilder.DefaultTitle(true), Parse(ReportBuilder.BuildMailto(r, To).Url).Fields["subject"]);
        }

        [Fact]
        public void MailtoValuesArePercentEncoded()
        {
            Assert.Equal("a%20b%26c%3Fd%3De%23f%25g%2Bh", ReportBuilder.EncodeMailtoValue("a b&c?d=e#f%g+h"));
            Assert.Equal("1%0D%0A2%0D%0A3%0D%0A4", ReportBuilder.EncodeMailtoValue("1\n2\r\n3\r4"));
            Assert.Equal("%D8%B9", ReportBuilder.EncodeMailtoValue("ع"));
            Assert.Equal("", ReportBuilder.EncodeMailtoValue(null));

            var url = ReportBuilder.BuildMailto(Bug(), To).Url;
            // Nothing in the subject or body can end the field early or be read as "+" = space.
            var query = url.Substring(url.IndexOf('?') + 1);
            Assert.Equal(2, query.Split('&').Length);
            Assert.DoesNotContain(" ", url);
            Assert.DoesNotContain("+", query);
            Assert.DoesNotContain("\n", url);
            Assert.Contains("%0D%0A", url);
        }

        [Fact]
        public void LongReportsAreShortenedToFitTheMailtoLimit()
        {
            var r = Bug(description: string.Concat(Enumerable.Repeat("وصف طويل جداً بالعربية ", 400)), steps: string.Concat(Enumerable.Repeat("خطوة ", 300)));
            var mail = ReportBuilder.BuildMailto(r, To);
            Assert.True(mail.Truncated);
            Assert.True(mail.Url.Length <= SupportContact.MaxMailtoLength, "length " + mail.Url.Length);
            // Close to the limit: as much of the report as fits is kept.
            Assert.True(mail.Url.Length > SupportContact.MaxMailtoLength - 40, "length " + mail.Url.Length);
            var body = Parse(mail.Url).Fields["body"];
            Assert.EndsWith(ReportBuilder.TruncatedNote.Replace("\n", "\r\n"), body);
            Assert.StartsWith(ReportBuilder.BuildText(r).Substring(0, 40).Replace("\n", "\r\n"), body);
            Assert.Equal(ReportBuilder.EffectiveTitle(r), Parse(mail.Url).Fields["subject"]);
            // The full text is still there for the clipboard.
            Assert.Contains(r.Description.Trim(), ReportBuilder.BuildText(r));
        }

        [Fact]
        public void TinyLimitsStillFit()
        {
            // Not a real limit, but the link never exceeds it: subject shortened, then the note dropped.
            var r = Bug(description: string.Concat(Enumerable.Repeat("وصف ", 200)));
            r.Title = string.Concat(Enumerable.Repeat("عنوان ", 30));
            foreach (var limit in new[] { 120, 300, 500 })
            {
                var mail = ReportBuilder.BuildMailto(r, To, limit);
                Assert.True(mail.Truncated);
                Assert.True(mail.Url.Length <= limit, limit + ": length " + mail.Url.Length);
                Assert.StartsWith("mailto:" + To + "?subject=", mail.Url);
            }
        }

        [Theory]
        [InlineData(700)]
        [InlineData(1000)]
        public void SmallerLimitsAreRespected(int limit)
        {
            var r = Bug(description: string.Concat(Enumerable.Repeat("long description ", 200)));
            r.Title = string.Concat(Enumerable.Repeat("عنوان ", 30));
            var mail = ReportBuilder.BuildMailto(r, To, limit);
            Assert.True(mail.Truncated);
            Assert.True(mail.Url.Length <= limit, limit + ": length " + mail.Url.Length);
            Assert.EndsWith(ReportBuilder.TruncatedNote.Trim().Replace("\n", "\r\n"), Parse(mail.Url).Fields["body"]);
        }

        [Fact]
        public void ShorteningNeverSplitsASurrogatePair()
        {
            var r = Bug(description: string.Concat(Enumerable.Repeat("🥝", 900)));
            for (var limit = 1200; limit < 1260; limit++)
            {
                var url = ReportBuilder.BuildMailto(r, To, limit).Url;
                Assert.True(url.Length <= limit, limit + ": length " + url.Length);
                var body = Parse(url).Fields["body"];
                Assert.DoesNotContain("\uFFFD", body);
                for (var i = 0; i < body.Length; i++)
                {
                    if (char.IsHighSurrogate(body[i])) Assert.True(i + 1 < body.Length && char.IsLowSurrogate(body[i + 1]), "high surrogate at " + i);
                    else if (char.IsLowSurrogate(body[i])) Assert.True(i > 0 && char.IsHighSurrogate(body[i - 1]), "low surrogate at " + i);
                }
            }
        }

        [Fact]
        public void ShortReportsAreNotTouched()
        {
            var mail = ReportBuilder.BuildMailto(Bug(), To);
            Assert.False(mail.Truncated);
            Assert.DoesNotContain(ReportBuilder.EncodeMailtoValue(ReportBuilder.TruncatedNote.Trim()), mail.Url);
        }

        [Fact]
        public void EnglishReportsUseTheEnglishNote()
        {
            TestStrings.With("en", () =>
            {
                var r = Bug(description: string.Concat(Enumerable.Repeat("a long description ", 300)));
                var body = Parse(ReportBuilder.BuildMailto(r, To).Url).Fields["body"];
                Assert.Contains("shortened to fit the link", body);
            });
        }
    }
}
