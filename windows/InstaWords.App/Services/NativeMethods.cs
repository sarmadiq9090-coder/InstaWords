using System;
using System.Runtime.InteropServices;
using System.Text;

namespace InstaWords.App.Services
{
    internal static class NativeMethods
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;
        public const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOPMOST = 0x00000008;

        public const int SW_RESTORE = 9;
        public const int ASFW_ANY = -1;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static int GetWindowLong(IntPtr hWnd, int index) =>
            IntPtr.Size == 8 ? (int)(long)GetWindowLongPtr64(hWnd, index) : GetWindowLong32(hWnd, index);

        public static void SetWindowLong(IntPtr hWnd, int index, int value)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(hWnd, index, new IntPtr(value));
            else SetWindowLong32(hWnd, index, value);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllowSetForegroundWindow(int dwProcessId);

        // Package identity (MSIX). Windows 8+; older systems throw EntryPointNotFoundException.
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, StringBuilder packageFamilyName);

        // Another process's exe path (single-instance hand-over). PROCESS_QUERY_LIMITED_INFORMATION is enough.
        public const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        /// <summary>The full path of a process's exe, or null when it cannot be read.</summary>
        public static string ProcessImagePath(int processId)
        {
            var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (handle == IntPtr.Zero) return null;
            try
            {
                var size = 1024;
                var buffer = new StringBuilder(size);
                return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : null;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        // Restart Manager (rstrtmgr.dll, Windows Vista+): closes the programs that use a file.
        public const uint RmForceShutdown = 0x1;

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        public static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

        [DllImport("rstrtmgr.dll")]
        public static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        public static extern int RmRegisterResources(uint pSessionHandle,
            uint nFiles, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] rgsFilenames,
            uint nApplications, IntPtr rgApplications,
            uint nServices, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        public static extern int RmShutdown(uint pSessionHandle, uint lActionFlags, IntPtr fnStatus);
    }
}
