using System;
using System.Globalization;

// Pure logic (no WPF, no Windows API): compiled into the app and linked into InstaWords.App.Tests.
// The Windows side is SingleInstance.cs (mutex, events, shared memory, Restart Manager).
namespace InstaWords.App.Services
{
    /// <summary>What a later launch does while another copy of InstaWords already runs.</summary>
    public enum LaunchAction
    {
        /// <summary>Bring the running copy's window up and exit: it is the same or a newer version, or its version is unknown.</summary>
        ShowRunning,
        /// <summary>The running copy is an older version: ask it to exit, then start in its place.</summary>
        TakeOver,
    }

    public static class InstanceLogic
    {
        /// <summary>Format marker of the text a running copy publishes about itself (bump it if the format changes).</summary>
        public const string InfoMarker = "InstaWords/1";

        /// <summary>"InstaWords/1 2.3.0 1234": the running copy's version and process id.</summary>
        public static string FormatInfo(Version version, int processId) =>
            InfoMarker + " " + UpdateLogic.Display(version) + " " + processId.ToString(CultureInfo.InvariantCulture);

        /// <summary>Reads <see cref="FormatInfo"/>'s text (trailing NULs from the shared memory are ignored).</summary>
        public static bool TryParseInfo(string text, out Version version, out int processId)
        {
            version = null;
            processId = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Trim('\0', ' ', '\r', '\n').Split(' ');
            if (parts.Length != 3 || parts[0] != InfoMarker) return false;
            if (!UpdateLogic.TryParseVersion(parts[1], out var v, out _)) return false;
            if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid <= 0) return false;
            version = UpdateLogic.Normalize(v);
            processId = pid;
            return true;
        }

        /// <summary>
        /// Only a strictly newer copy takes over. The same version, an older one, or an unknown version on
        /// either side keeps today's behaviour: the running copy is shown and nothing is closed.
        /// </summary>
        public static LaunchAction Decide(Version running, Version mine)
        {
            if (running == null || mine == null) return LaunchAction.ShowRunning;
            return UpdateLogic.Normalize(mine).CompareTo(UpdateLogic.Normalize(running)) > 0 ? LaunchAction.TakeOver : LaunchAction.ShowRunning;
        }
    }
}
