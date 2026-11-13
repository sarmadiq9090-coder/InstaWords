using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace InstaWords.App.Services
{
    /// <summary>
    /// One InstaWords per user session (named Mutex). A later launch either shows the running copy
    /// (through a named event, then exits) or, when the running copy is an OLDER version, takes its place:
    ///   - the running copy publishes its version in a small named shared-memory block (".Info");
    ///   - a newer launch sets the ".Exit" event: the running copy saves and exits as if Exit was chosen in
    ///     the tray, which releases the mutex, and the newer copy starts;
    ///   - copies from before this protocol (2.2.0 and older) publish nothing: their version is read from
    ///     their exe file, and they are asked to close through the Restart Manager, the way Windows asks
    ///     at sign-out (they save their settings and history on that request).
    /// The same or a newer version running, or anything unclear, keeps the old behaviour: show it, close nothing.
    /// </summary>
    public sealed class SingleInstance : IDisposable
    {
        private const int InfoSize = 256;
        /// <summary>How long a newer copy waits for the older one to exit before showing it instead.</summary>
        private static readonly TimeSpan HandOverTimeout = TimeSpan.FromSeconds(20);

        private readonly string _name;
        private readonly Mutex _mutex;
        private readonly EventWaitHandle _activate;
        private readonly EventWaitHandle _exit;
        private MemoryMappedFile _info;
        private RegisteredWaitHandle _wait;
        private RegisteredWaitHandle _exitWait;

        private SingleInstance(string name, Mutex mutex, EventWaitHandle activate, EventWaitHandle exit, bool isFirst)
        {
            _name = name;
            _mutex = mutex;
            _activate = activate;
            _exit = exit;
            IsFirst = isFirst;
        }

        /// <summary>True when this process owns the mutex (it started first, or took over from an older copy).</summary>
        public bool IsFirst { get; private set; }

        public static SingleInstance Acquire()
        {
            var name = "FastKiwi.InstaWords";
            if (AppPaths.IsIsolated) name += "." + Hash(AppPaths.DataDir); // tests run side by side with a real copy
            // The installer waits on this exact mutex name (InstaWords.iss AppMutexName): keep it.
            var mutex = new Mutex(true, @"Local\" + name + ".Instance", out var createdNew);
            var activate = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + name + ".Activate");
            var exit = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + name + ".Exit");
            return new SingleInstance(name, mutex, activate, exit, createdNew);
        }

        private string InfoName => @"Local\" + _name + ".Info";

        /// <summary>Second instance: let the first one take the foreground and wake it up.</summary>
        public void SignalFirst()
        {
            NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
            _activate.Set();
        }

        /// <summary>
        /// Second launch: when the running copy is older than <paramref name="mine"/>, asks it to exit and waits
        /// until this process owns the mutex (<see cref="IsFirst"/> becomes true). False: show the running copy instead.
        /// </summary>
        public bool TryTakeOver(Version mine)
        {
            if (IsFirst) return true;
            bool asked;
            try
            {
                if (TryReadInfo(out var running, out var pid))
                {
                    if (InstanceLogic.Decide(running, mine) != LaunchAction.TakeOver) return false;
                    Log.Warn("Hand-over: asking InstaWords " + UpdateLogic.Display(running) + " (process " + pid + ") to exit for " + UpdateLogic.Display(mine));
                    _exit.Set();
                    asked = true;
                }
                else asked = TryCloseOlderCopy(mine);
            }
            catch (Exception ex)
            {
                Log.Warn("Hand-over: " + ex.Message);
                return false;
            }
            if (!asked) return false;
            var owned = WaitForMutex(HandOverTimeout);
            if (!owned) Log.Warn("Hand-over: the older copy did not exit in time; showing it instead");
            return owned;
        }

        private bool WaitForMutex(TimeSpan timeout)
        {
            try
            {
                IsFirst = _mutex.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                IsFirst = true; // the older copy ended without releasing it: the wait still gave it to us
            }
            return IsFirst;
        }

        /// <summary>First instance: tell later launches which version runs here.</summary>
        public void Publish(Version version)
        {
            if (!IsFirst) return;
            try
            {
                var text = Encoding.ASCII.GetBytes(InstanceLogic.FormatInfo(version, Process.GetCurrentProcess().Id));
                var buffer = new byte[InfoSize];
                Array.Copy(text, buffer, Math.Min(text.Length, InfoSize - 1));
                _info = MemoryMappedFile.CreateOrOpen(InfoName, InfoSize, MemoryMappedFileAccess.ReadWrite);
                using (var view = _info.CreateViewAccessor(0, InfoSize))
                    view.WriteArray(0, buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Log.Warn("Instance info: " + ex.Message);
            }
        }

        private bool TryReadInfo(out Version version, out int processId)
        {
            version = null;
            processId = 0;
            try
            {
                using (var map = MemoryMappedFile.OpenExisting(InfoName, MemoryMappedFileRights.Read))
                using (var view = map.CreateViewAccessor(0, InfoSize, MemoryMappedFileAccess.Read))
                {
                    var buffer = new byte[InfoSize];
                    view.ReadArray(0, buffer, 0, buffer.Length);
                    return InstanceLogic.TryParseInfo(Encoding.ASCII.GetString(buffer), out version, out processId);
                }
            }
            catch (FileNotFoundException)
            {
                return false; // nothing published: a copy from before this protocol (or one still starting)
            }
        }

        /// <summary>
        /// A running copy that publishes nothing (2.2.0 and older): exactly one other InstaWords process in this
        /// session, older by its exe's version, at another path. Asked to close through the Restart Manager.
        /// </summary>
        private static bool TryCloseOlderCopy(Version mine)
        {
            if (AppPaths.IsIsolated) return false; // test copies never close another process
            var path = FindOtherCopy();
            if (path == null) return false;
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(AppPaths.ExePath), StringComparison.OrdinalIgnoreCase)) return false;
            var running = FileVersionOf(path);
            if (InstanceLogic.Decide(running, mine) != LaunchAction.TakeOver) return false;
            Log.Warn("Hand-over: closing InstaWords " + UpdateLogic.Display(running) + " at " + path + " for " + UpdateLogic.Display(mine));
            return RestartManager.CloseProcessesUsing(path);
        }

        private static string FindOtherCopy()
        {
            string found = null;
            using (var me = Process.GetCurrentProcess())
            {
                foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppPaths.ExePath)))
                {
                    using (p)
                    {
                        if (p.Id == me.Id || p.SessionId != me.SessionId) continue;
                        var path = NativeMethods.ProcessImagePath(p.Id);
                        if (path == null) continue;
                        // Several different copies at once: do not guess which one holds the mutex.
                        if (found != null && !string.Equals(found, path, StringComparison.OrdinalIgnoreCase)) return null;
                        found = path;
                    }
                }
            }
            return found;
        }

        private static Version FileVersionOf(string path)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (UpdateLogic.TryParseVersion(info.ProductVersion, out var v, out _)) return UpdateLogic.Normalize(v);
                if (UpdateLogic.TryParseVersion(info.FileVersion, out v, out _)) return UpdateLogic.Normalize(v);
            }
            catch (Exception ex)
            {
                Log.Warn("Version of " + path + ": " + ex.Message);
            }
            return null;
        }

        /// <summary>First instance: call <paramref name="onActivate"/> (on a pool thread) whenever another launch signals.</summary>
        public void Listen(Action onActivate)
        {
            _wait = ThreadPool.RegisterWaitForSingleObject(_activate, (s, timedOut) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
        }

        /// <summary>First instance: call <paramref name="onExit"/> (on a pool thread) once a newer copy asks this one to make way.</summary>
        public void ListenForExit(Action onExit)
        {
            if (_exitWait != null) return;
            _exitWait = ThreadPool.RegisterWaitForSingleObject(_exit, (s, timedOut) => onExit(), null, Timeout.Infinite, executeOnlyOnce: true);
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
            _exitWait?.Unregister(null);
            // Before the mutex: a newer copy that gets the mutex must never read this copy's version.
            _info?.Dispose();
            _info = null;
            if (IsFirst)
            {
                try { _mutex.ReleaseMutex(); } catch (ApplicationException) { /* not owned */ }
            }
            _mutex.Dispose();
            _activate.Dispose();
            _exit.Dispose();
        }
    }

    /// <summary>
    /// Windows Restart Manager: closes the programs that use a file the way Windows does at sign-out
    /// (WM_QUERYENDSESSION, then WM_ENDSESSION; a program that does not answer is ended after a timeout).
    /// Inno Setup's CloseApplications uses the same API.
    /// </summary>
    internal static class RestartManager
    {
        public static bool CloseProcessesUsing(string path)
        {
            var key = new StringBuilder(64);
            if (NativeMethods.RmStartSession(out var session, 0, key) != 0) return false;
            try
            {
                if (NativeMethods.RmRegisterResources(session, 1, new[] { path }, 0, IntPtr.Zero, 0, null) != 0) return false;
                var rc = NativeMethods.RmShutdown(session, NativeMethods.RmForceShutdown, IntPtr.Zero);
                if (rc != 0) Log.Warn("Restart Manager could not close " + path + " (error " + rc + ")");
                return rc == 0;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                return false;
            }
            finally
            {
                try { NativeMethods.RmEndSession(session); } catch { /* ignore */ }
            }
        }
    }
}
