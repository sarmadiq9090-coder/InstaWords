using System;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace InstaWords.App.Services
{
    /// <summary>Notification-area icon with the فتح / تحويل الحافظة / الإعدادات / خروج menu.</summary>
    public sealed class TrayService : IDisposable
    {
        private readonly Forms.NotifyIcon _icon;
        private readonly Forms.ToolStripMenuItem _convertItem;

        public TrayService()
        {
            var menu = new Forms.ContextMenuStrip
            {
                RightToLeft = Forms.RightToLeft.Yes,
                ShowImageMargin = false,
                Font = new Font("Segoe UI", 10f),
                Renderer = new Forms.ToolStripProfessionalRenderer(new TrayColors()),
            };
            var open = new Forms.ToolStripMenuItem("فتح InstaWords", null, (s, e) => OpenRequested?.Invoke(this, EventArgs.Empty))
            {
                Font = new Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold),
            };
            _convertItem = new Forms.ToolStripMenuItem("تحويل الحافظة", null, (s, e) => ConvertRequested?.Invoke(this, EventArgs.Empty));
            var settings = new Forms.ToolStripMenuItem("الإعدادات", null, (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty));
            var update = new Forms.ToolStripMenuItem("التحقق من وجود تحديث", null, (s, e) => UpdateRequested?.Invoke(this, EventArgs.Empty));
            var report = new Forms.ToolStripMenuItem("الإبلاغ عن مشكلة أو اقتراح", null, (s, e) => ReportRequested?.Invoke(this, EventArgs.Empty));
            var exit = new Forms.ToolStripMenuItem("خروج", null, (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty));
            foreach (Forms.ToolStripItem item in new Forms.ToolStripItem[] { open, _convertItem, settings, update, report, exit })
                item.Padding = new Forms.Padding(4, 3, 4, 3);
            menu.Items.AddRange(new Forms.ToolStripItem[]
            {
                open, _convertItem, settings, new Forms.ToolStripSeparator(), update, report, new Forms.ToolStripSeparator(), exit,
            });

            _icon = new Forms.NotifyIcon
            {
                Text = "InstaWords — تفقيط",
                Icon = LoadIcon(),
                ContextMenuStrip = menu,
                Visible = true,
            };
            _icon.MouseClick += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left) OpenRequested?.Invoke(this, EventArgs.Empty);
            };
        }

        public event EventHandler OpenRequested;
        public event EventHandler ConvertRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler ExitRequested;
        public event EventHandler UpdateRequested;
        public event EventHandler ReportRequested;

        /// <summary>Shows the current hotkey next to «تحويل الحافظة».</summary>
        public void SetHotkeyText(string gesture) => _convertItem.ShortcutKeyDisplayString = gesture ?? "";

        private static Icon LoadIcon()
        {
            try
            {
                var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/InstaWords.ico"));
                if (info != null)
                    using (var s = info.Stream)
                        return new Icon(s, Forms.SystemInformation.SmallIconSize);
            }
            catch (Exception ex)
            {
                Log.Warn("Tray icon: " + ex.Message);
            }
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.ContextMenuStrip?.Dispose();
            _icon.Dispose();
        }

        /// <summary>Menu colours in the FastKiwi palette.</summary>
        private sealed class TrayColors : Forms.ProfessionalColorTable
        {
            private static readonly Color Hover = Color.FromArgb(0xEA, 0xF4, 0xDF);
            private static readonly Color Line = Color.FromArgb(0xE2, 0xEA, 0xD8);
            public override Color MenuItemSelected => Hover;
            public override Color MenuItemSelectedGradientBegin => Hover;
            public override Color MenuItemSelectedGradientEnd => Hover;
            public override Color MenuItemBorder => Color.FromArgb(0xC9, 0xD6, 0xBB);
            public override Color MenuBorder => Line;
            public override Color ToolStripDropDownBackground => Color.White;
            public override Color ImageMarginGradientBegin => Color.White;
            public override Color ImageMarginGradientMiddle => Color.White;
            public override Color ImageMarginGradientEnd => Color.White;
            public override Color SeparatorDark => Line;
            public override Color SeparatorLight => Color.White;
        }
    }
}
