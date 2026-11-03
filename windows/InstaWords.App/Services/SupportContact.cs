using System;
using System.Text.RegularExpressions;

// Pure logic (no WPF): compiled into the app and linked into InstaWords.App.Tests.
namespace InstaWords.App.Services
{
    /// <summary>
    /// How to reach FastKiwi without a GitHub account (the first, primary option of
    /// «الإبلاغ عن مشكلة أو اقتراح»). Nothing is ever sent automatically: the report opens in the
    /// user's own mail app or browser and they send it themselves.
    /// </summary>
    public static class SupportContact
    {
        // TODO(owner): fill these in once the support mailbox and the Telegram account exist.
        //   Email:    e.g. "support@fastkiwi.app". Set → «إرسال بالبريد الإلكتروني» opens a prefilled mailto: message.
        //   Telegram: e.g. "fastkiwi", "@fastkiwi" or "https://t.me/fastkiwi". Set → an extra «عبر Telegram» button.
        // While Email is empty the primary button copies the report and opens the support page instead.
        public const string Email = "";
        public const string Telegram = "";

        /// <summary>The site's support page (explains how to reach FastKiwi without GitHub). English at the root, Arabic under /ar/.</summary>
        public const string SupportPageEn = "https://fastkiwi.app/support/";
        public const string SupportPageAr = "https://fastkiwi.app/ar/support/";

        /// <summary>Longest mailto: link handed to the mail app (Windows/Outlook drop or cut links past ~2,000 characters).</summary>
        public const int MaxMailtoLength = 2000;

        private static readonly Regex EmailShape = new Regex(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$");
        private static readonly Regex TelegramName = new Regex(@"^[A-Za-z][A-Za-z0-9_]{4,31}$");

        /// <summary>True for a value nobody has filled in yet: empty, whitespace, a TODO or an example address.</summary>
        public static bool IsPlaceholder(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            var v = value.Trim();
            return v.IndexOf("TODO", StringComparison.OrdinalIgnoreCase) >= 0
                   || v.IndexOf("example.", StringComparison.OrdinalIgnoreCase) >= 0
                   || v.Contains("<") || v.Contains(">");
        }

        /// <summary>The address to write to, or null when <paramref name="email"/> is a placeholder or not an address.</summary>
        public static string EmailAddress(string email)
        {
            if (IsPlaceholder(email)) return null;
            var v = email.Trim();
            if (v.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) v = v.Substring(7);
            return EmailShape.IsMatch(v) ? v : null;
        }

        /// <summary>The https://t.me/ link for a Telegram name or link, or null when it is a placeholder or not valid.</summary>
        public static string TelegramUrl(string telegram)
        {
            if (IsPlaceholder(telegram)) return null;
            var v = telegram.Trim().TrimEnd('/');
            foreach (var prefix in new[] { "https://t.me/", "http://t.me/", "https://telegram.me/", "t.me/", "@" })
                if (v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { v = v.Substring(prefix.Length); break; }
            return TelegramName.IsMatch(v) ? "https://t.me/" + v : null;
        }

        /// <summary>The support page in the interface language ("ar" → /ar/support/, otherwise /support/).</summary>
        public static string SupportPage(string language) => language == Loc.English ? SupportPageEn : SupportPageAr;

        /// <summary>The configured address (null until <see cref="Email"/> is filled in).</summary>
        public static string ConfiguredEmail => EmailAddress(Email);

        /// <summary>The configured Telegram link (null until <see cref="Telegram"/> is filled in).</summary>
        public static string ConfiguredTelegram => TelegramUrl(Telegram);
    }

    /// <summary>A mailto: link and whether the body had to be shortened to fit it.</summary>
    public sealed class MailtoLink
    {
        public string Url { get; set; }
        public bool Truncated { get; set; }
    }
}
