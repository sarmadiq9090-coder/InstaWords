using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace InstaWords.App.Services
{
    /// <summary>
    /// One system-wide hotkey (RegisterHotKey) on a hidden message-only window, so it keeps
    /// working while the main window is hidden in the tray.
    /// </summary>
    public sealed class HotkeyService : IDisposable
    {
        private const int HotkeyId = 0x7A51;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);
        private readonly HwndSource _source;
        private bool _registered;

        public HotkeyService()
        {
            var p = new HwndSourceParameters("InstaWordsHotkeySink") { Width = 0, Height = 0, WindowStyle = 0, ParentWindow = HWND_MESSAGE };
            _source = new HwndSource(p);
            _source.AddHook(WndProc);
        }

        public event EventHandler Pressed;

        public HotkeyGesture Current { get; private set; }
        public bool IsRegistered => _registered;
        /// <summary>Why the last Register failed, in the interface language (null on success).</summary>
        public string LastError { get; private set; }

        public bool Register(HotkeyGesture gesture)
        {
            Unregister();
            Current = gesture;
            LastError = null;
            if (gesture == null || !gesture.IsValid)
            {
                LastError = Loc.T("Hotkey.Invalid");
                return false;
            }
            if (NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, gesture.NativeModifiers, gesture.VirtualKey))
            {
                _registered = true;
                return true;
            }
            var err = Marshal.GetLastWin32Error();
            LastError = err == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
                ? Loc.F("Hotkey.Taken", gesture)
                : Loc.F("Hotkey.RegisterFailed", gesture, new Win32Exception(err).Message);
            return false;
        }

        public void Unregister()
        {
            if (!_registered) return;
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                handled = true;
                Pressed?.Invoke(this, EventArgs.Empty);
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            _source.RemoveHook(WndProc);
            _source.Dispose();
        }
    }
}
