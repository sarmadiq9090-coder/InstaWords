using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace InstaWords.App.Services
{
    /// <summary>A global shortcut such as Ctrl+Alt+T, stored as text in the settings.</summary>
    public sealed class HotkeyGesture : IEquatable<HotkeyGesture>
    {
        public static readonly HotkeyGesture Default = new HotkeyGesture(ModifierKeys.Control | ModifierKeys.Alt, Key.T);

        public HotkeyGesture(ModifierKeys modifiers, Key key)
        {
            Modifiers = modifiers;
            Key = key;
        }

        public ModifierKeys Modifiers { get; }
        public Key Key { get; }

        /// <summary>At least Ctrl, Alt or Win, plus a non-modifier key (Shift alone would block typing).</summary>
        public bool IsValid =>
            (Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0 && !IsModifierKey(Key) && Key != Key.None;

        public uint NativeModifiers
        {
            get
            {
                uint m = NativeMethods.MOD_NOREPEAT;
                if ((Modifiers & ModifierKeys.Control) != 0) m |= NativeMethods.MOD_CONTROL;
                if ((Modifiers & ModifierKeys.Alt) != 0) m |= NativeMethods.MOD_ALT;
                if ((Modifiers & ModifierKeys.Shift) != 0) m |= NativeMethods.MOD_SHIFT;
                if ((Modifiers & ModifierKeys.Windows) != 0) m |= NativeMethods.MOD_WIN;
                return m;
            }
        }

        public uint VirtualKey => (uint)KeyInterop.VirtualKeyFromKey(Key);

        public static bool IsModifierKey(Key k) =>
            k == Key.LeftCtrl || k == Key.RightCtrl || k == Key.LeftAlt || k == Key.RightAlt ||
            k == Key.LeftShift || k == Key.RightShift || k == Key.LWin || k == Key.RWin || k == Key.System;

        public override string ToString()
        {
            var parts = new List<string>();
            if ((Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
            parts.Add(KeyName(Key));
            return string.Join("+", parts);
        }

        private static string KeyName(Key k)
        {
            if (k >= Key.D0 && k <= Key.D9) return ((char)('0' + (k - Key.D0))).ToString();
            if (k >= Key.NumPad0 && k <= Key.NumPad9) return "Num" + (k - Key.NumPad0);
            switch (k)
            {
                case Key.OemPlus: return "=";
                case Key.OemMinus: return "-";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.Space: return "Space";
                case Key.Return: return "Enter";
                default: return k.ToString();
            }
        }

        public static HotkeyGesture Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var mods = ModifierKeys.None;
            Key key = Key.None;
            foreach (var raw in text.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0))
            {
                switch (raw.ToLowerInvariant())
                {
                    case "ctrl": case "control": mods |= ModifierKeys.Control; continue;
                    case "alt": mods |= ModifierKeys.Alt; continue;
                    case "shift": mods |= ModifierKeys.Shift; continue;
                    case "win": case "windows": mods |= ModifierKeys.Windows; continue;
                }
                if (raw.Length == 1 && raw[0] >= '0' && raw[0] <= '9') key = Key.D0 + (raw[0] - '0');
                else if (raw == "=") key = Key.OemPlus;
                else if (raw == "-") key = Key.OemMinus;
                else if (raw == ",") key = Key.OemComma;
                else if (raw == ".") key = Key.OemPeriod;
                else if (raw.StartsWith("Num", StringComparison.OrdinalIgnoreCase) && raw.Length == 4 && char.IsDigit(raw[3])) key = Key.NumPad0 + (raw[3] - '0');
                else if (raw.Equals("Enter", StringComparison.OrdinalIgnoreCase)) key = Key.Return;
                else if (!Enum.TryParse(raw, true, out key)) return null;
            }
            var g = new HotkeyGesture(mods, key);
            return g.IsValid ? g : null;
        }

        public bool Equals(HotkeyGesture other) => other != null && other.Modifiers == Modifiers && other.Key == Key;
        public override bool Equals(object obj) => Equals(obj as HotkeyGesture);
        public override int GetHashCode() => ((int)Modifiers * 397) ^ (int)Key;
    }
}
