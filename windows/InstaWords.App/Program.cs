using System;
using System.Threading.Tasks;
using InstaWords.App.Services;

namespace InstaWords.App
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            InstallCrashHandlers();
            using (var instance = SingleInstance.Acquire())
            {
                // Already running (maybe hidden in the tray): an older version makes way for this one;
                // the same or a newer version is brought up instead.
                if (!instance.IsFirst && !instance.TryTakeOver(UpdateService.CurrentVersion))
                {
                    instance.SignalFirst();
                    return 0;
                }
                instance.Publish(UpdateService.CurrentVersion);
                var app = new App
                {
                    StartHidden = StartupService.IsStartupLaunch(args),
                    AfterUpdate = HasArgument(args, UpdateService.UpdatedArgument),
                    AfterFailedUpdate = HasArgument(args, UpdateService.UpdateFailedArgument),
                };
                instance.Listen(() => app.Dispatcher.BeginInvoke(new Action(() => app.ShowMainWindow())));
                // Only once the app has started (its settings are loaded), so it saves them on the way out.
                app.Startup += (s, e) => instance.ListenForExit(() => app.Dispatcher.BeginInvoke(new Action(() => app.ExitApp())));
                app.InitializeComponent();
                return app.Run();
            }
        }

        /// <summary>
        /// Errors outside the UI thread: App's DispatcherUnhandledException only sees the UI thread.
        /// A crash on any other thread ends the process (.NET cannot stop that), so it is at least logged;
        /// an unobserved faulted Task is logged and marked observed so it never takes the app down.
        /// </summary>
        private static void InstallCrashHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception
                         ?? new Exception("Non-exception object thrown: " + e.ExceptionObject);
                Log.Warn("Unhandled (AppDomain): " + ex);
                CrashLog.Write(AppPaths.DataDir, CrashLog.Source.AppDomain, ex, VersionText(), e.IsTerminating);
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log.Warn("Unobserved task: " + e.Exception);
                CrashLog.Write(AppPaths.DataDir, CrashLog.Source.Task, e.Exception, VersionText());
                e.SetObserved();
            };
        }

        private static bool HasArgument(string[] args, string name) =>
            args != null && Array.Exists(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

        internal static string VersionText()
        {
            try { return UpdateService.CurrentVersionText; }
            catch { return null; }
        }
    }
}
