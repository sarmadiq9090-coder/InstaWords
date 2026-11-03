using System;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace InstaWords.App.Services
{
    /// <summary>
    /// «تشغيل مع Windows». Installed and portable copies: the per-user Run key (no admin rights needed).
    /// The Store copy (MSIX) cannot use the Run key (its registry writes stay inside the package), so it
    /// uses the package's startup task (uap5:StartupTask in windows/packaging/msix/AppxManifest.xml),
    /// which the user also sees in Task Manager → Startup apps.
    /// </summary>
    public static class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ValueName = "InstaWords";
        public const string TrayArgument = "--tray";

        private static string Command => "\"" + AppPaths.ExePath + "\" " + TrayArgument;

        /// <summary>Started by Windows at sign-in: --tray from the Run key, or the Store copy's startup task.</summary>
        public static bool IsStartupLaunch(string[] args)
        {
            if (args != null && Array.IndexOf(args, TrayArgument) >= 0) return true;
            return AppPackage.IsPackaged && StoreStartupTask.WasActivatedByStartupTask();
        }

        /// <summary>What the check box shows now.</summary>
        public static Task<StartupView> GetViewAsync()
        {
            if (AppPackage.IsPackaged) return StoreStartupTask.GetViewAsync();
            return Task.FromResult(new StartupView(IsEnabled, true, null));
        }

        /// <summary>Turns starting with Windows on or off, then returns what the check box shows.</summary>
        public static Task<StartupView> SetEnabledAsync(bool enabled)
        {
            if (AppPackage.IsPackaged) return StoreStartupTask.SetEnabledAsync(enabled);
            SetEnabled(enabled);
            return Task.FromResult(new StartupView(IsEnabled, true, null));
        }

        // ---------- Run key (installed and portable copies) ----------

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                        return key?.GetValue(ValueName) is string v && v.IndexOf(AppPaths.ExePath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch { return false; }
            }
        }

        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey, true))
                {
                    if (key == null) return false;
                    if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
                    else if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Run key: " + ex.Message);
                return false;
            }
        }
    }

    /// <summary>
    /// The Store copy's startup task, through the WinRT StartupTask API (Microsoft.Windows.SDK.Contracts:
    /// reference metadata only; Windows 10/11 provide the runtime). Only called inside the package.
    /// </summary>
    internal static class StoreStartupTask
    {
        public static async Task<StartupView> GetViewAsync()
        {
            try
            {
                var task = await Windows.ApplicationModel.StartupTask.GetAsync(StoreLogic.StartupTaskId);
                return StoreLogic.DescribeStartupTask((StartupTaskStatus)(int)task.State);
            }
            catch (Exception ex)
            {
                Log.Warn("Startup task: " + ex.Message);
                return new StartupView(false, false, null);
            }
        }

        public static async Task<StartupView> SetEnabledAsync(bool enabled)
        {
            try
            {
                var task = await Windows.ApplicationModel.StartupTask.GetAsync(StoreLogic.StartupTaskId);
                if (enabled)
                {
                    // Packaged desktop apps get no consent dialog: this enables the task unless the user
                    // turned it off in Task Manager / Settings (then only they can turn it back on there).
                    if (task.State == Windows.ApplicationModel.StartupTaskState.Disabled) await task.RequestEnableAsync();
                }
                else if (task.State == Windows.ApplicationModel.StartupTaskState.Enabled) task.Disable();
                return StoreLogic.DescribeStartupTask((StartupTaskStatus)(int)task.State);
            }
            catch (Exception ex)
            {
                Log.Warn("Startup task: " + ex.Message);
                return await GetViewAsync();
            }
        }

        /// <summary>The startup task launches the app with no arguments: ask how it was activated.</summary>
        public static bool WasActivatedByStartupTask()
        {
            try
            {
                var args = Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
                return args != null && args.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
            }
            catch (Exception ex)
            {
                Log.Warn("Activation kind: " + ex.Message);
                return false; // show the window: the safe default
            }
        }
    }
}
