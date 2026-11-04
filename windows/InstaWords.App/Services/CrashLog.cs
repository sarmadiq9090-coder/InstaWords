using System;
using System.Globalization;
using System.IO;
using System.Text;

// Pure logic (no WPF): compiled into the app and linked into InstaWords.App.Tests.
namespace InstaWords.App.Services
{
    /// <summary>
    /// Appends unhandled exceptions to crash.log in the data folder (%APPDATA%\FastKiwi\InstaWords).
    /// Local only: nothing is sent anywhere. The entry holds the time, the app version, where the error
    /// was caught and the exception's type, message and stack trace (never amounts, clipboard or history).
    /// The file is capped: past <see cref="MaxBytes"/> it is moved to crash.old.log and a new one starts.
    /// Writing never throws, because it runs on the error path.
    /// </summary>
    public static class CrashLog
    {
        public const string FileName = "crash.log";
        public const string OldFileName = "crash.old.log";
        public const long MaxBytes = 256 * 1024;
        private static readonly object Gate = new object();

        /// <summary>Where the error was caught; written into the entry.</summary>
        public enum Source { Dispatcher, AppDomain, Task }

        public static string FormatEntry(Source source, Exception ex, string version, DateTime utcNow, bool terminating = false)
        {
            var sb = new StringBuilder();
            sb.Append("==== ").Append(utcNow.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture))
              .Append(" · InstaWords ").Append(string.IsNullOrEmpty(version) ? "?" : version)
              .Append(" · ").Append(source.ToString());
            if (terminating) sb.Append(" (terminating)");
            sb.Append('\n');
            sb.Append(ex == null ? "(no exception object)" : ex.ToString().Replace("\r\n", "\n")).Append("\n\n");
            return sb.ToString();
        }

        /// <summary>Appends one entry to <c>dir\crash.log</c>; returns false (never throws) if it could not.</summary>
        public static bool Write(string dir, Source source, Exception ex, string version, bool terminating = false)
        {
            try
            {
                var entry = FormatEntry(source, ex, version, DateTime.UtcNow, terminating);
                lock (Gate)
                {
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, FileName);
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length + entry.Length > MaxBytes)
                    {
                        var old = Path.Combine(dir, OldFileName);
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    File.AppendAllText(path, entry, new UTF8Encoding(false));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
