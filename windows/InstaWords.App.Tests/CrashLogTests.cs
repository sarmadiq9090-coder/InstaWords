using System;
using System.IO;
using InstaWords.App.Services;
using Xunit;

namespace InstaWords.App.Tests
{
    public class CrashLogTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "iw-crash-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private static Exception Thrown()
        {
            try { throw new InvalidOperationException("boom"); }
            catch (Exception ex) { return ex; }
        }

        [Fact]
        public void EntryHasTimeVersionSourceAndStack()
        {
            var text = CrashLog.FormatEntry(CrashLog.Source.AppDomain, Thrown(), "2.1.0",
                new DateTime(2026, 11, 3, 17, 5, 9, DateTimeKind.Utc), terminating: true);
            Assert.StartsWith("==== 2026-11-03 17:05:09Z · InstaWords 2.1.0 · AppDomain (terminating)\n", text);
            Assert.Contains("System.InvalidOperationException: boom", text);
            Assert.Contains(nameof(Thrown), text);
            Assert.DoesNotContain("\r", text);
            Assert.EndsWith("\n\n", text);
        }

        [Fact]
        public void NullExceptionAndVersionAreTolerated()
        {
            var text = CrashLog.FormatEntry(CrashLog.Source.Task, null, null, DateTime.UtcNow);
            Assert.Contains("InstaWords ? · Task\n(no exception object)", text);
        }

        [Fact]
        public void WriteCreatesFolderAndAppends()
        {
            Assert.True(CrashLog.Write(_dir, CrashLog.Source.Dispatcher, Thrown(), "2.1.0"));
            Assert.True(CrashLog.Write(_dir, CrashLog.Source.Task, Thrown(), "2.1.0"));
            var text = File.ReadAllText(Path.Combine(_dir, CrashLog.FileName));
            Assert.Contains("· Dispatcher\n", text);
            Assert.Contains("· Task\n", text);
        }

        [Fact]
        public void RotatesPastTheCap()
        {
            Directory.CreateDirectory(_dir);
            var path = Path.Combine(_dir, CrashLog.FileName);
            File.WriteAllText(path, new string('x', (int)CrashLog.MaxBytes));
            Assert.True(CrashLog.Write(_dir, CrashLog.Source.Task, Thrown(), "2.1.0"));
            Assert.True(File.Exists(Path.Combine(_dir, CrashLog.OldFileName)));
            Assert.True(new FileInfo(path).Length < CrashLog.MaxBytes);
            Assert.Contains("boom", File.ReadAllText(path));
        }

        [Fact]
        public void NeverThrowsOnABadFolder()
        {
            Directory.CreateDirectory(_dir);
            var blocker = Path.Combine(_dir, "file");
            File.WriteAllText(blocker, "");
            Assert.False(CrashLog.Write(Path.Combine(blocker, "sub"), CrashLog.Source.Task, Thrown(), "2.1.0"));
        }
    }
}
