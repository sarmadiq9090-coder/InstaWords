using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace InstaWords.App.Services
{
    /// <summary>
    /// One InstaWords per user session (named Mutex). A second launch signals the first
    /// through a named event, so the running copy shows its window, then exits.
    /// </summary>
    public sealed class SingleInstance : IDisposable
    {
        private readonly Mutex _mutex;
        private readonly EventWaitHandle _activate;
        private RegisteredWaitHandle _wait;

        private SingleInstance(Mutex mutex, EventWaitHandle activate, bool isFirst)
        {
            _mutex = mutex;
            _activate = activate;
            IsFirst = isFirst;
        }

        public bool IsFirst { get; }

        public static SingleInstance Acquire()
        {
            var name = "FastKiwi.InstaWords";
            if (AppPaths.IsIsolated) name += "." + Hash(AppPaths.DataDir); // tests run side by side with a real copy
            var mutex = new Mutex(true, @"Local\" + name + ".Instance", out var createdNew);
            var activate = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + name + ".Activate");
            return new SingleInstance(mutex, activate, createdNew);
        }

        /// <summary>Second instance: let the first one take the foreground and wake it up.</summary>
        public void SignalFirst()
        {
            NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
            _activate.Set();
        }

        /// <summary>First instance: call <paramref name="onActivate"/> (on a pool thread) whenever another launch signals.</summary>
        public void Listen(Action onActivate)
        {
            _wait = ThreadPool.RegisterWaitForSingleObject(_activate, (s, timedOut) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
        }

        private static string Hash(string s)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s.ToLowerInvariant()));
                return BitConverter.ToString(bytes, 0, 6).Replace("-", "");
            }
        }

        public void Dispose()
        {
            _wait?.Unregister(null);
            if (IsFirst)
            {
                try { _mutex.ReleaseMutex(); } catch (ApplicationException) { /* not owned */ }
            }
            _mutex.Dispose();
            _activate.Dispose();
        }
    }
}
