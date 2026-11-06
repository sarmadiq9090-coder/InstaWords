using System;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace InstaWords.App.Services
{
    /// <summary>Notification-area icon with the open / convert clipboard / settings / exit menu.</summary>
    public sealed class TrayService : IDisposable
    {
        private readonly Forms.NotifyIcon _icon;
        private readonly Forms.ContextMenuStrip _menu;
        private readonly Forms.ToolStripMenuItem _openItem;
        private readonly Forms.ToolStripMenuItem _convertItem;
        private readonly Forms.ToolStripMenuItem _settingsItem;
        private readonly Forms.ToolStripMenuItem _updateItem;
        private readonly Forms.ToolStripMenuItem _reportItem;
        private readonly Forms.ToolStripMenuItem _exitItem;

        public TrayService()
        {
            _menu = new Forms.ContextMenuStrip
            {
                ShowImageMargin = false,
                Font = new Font("Segoe UI", 10f),
                Renderer = new Forms.ToolStripProfessionalRenderer(new TrayColors()),
            };
            _openItem = new Forms.ToolStripMenuItem("", null, (s, e) => OpenRequested?.Invoke(this, EventArgs.Empty))
            {
                Font = new Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold),
            };
            _convertItem = new Forms.ToolStripMenuItem("", null, (s, e) => ConvertRequested?.Invoke(this, EventArgs.Empty));
            _settingsItem = new Forms.ToolStripMenuItem("", null, (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty));
            _updateItem = new Forms.ToolStripMenuItem("", null, (s, e) => UpdateRequested?.Invoke(this, EventArgs.Empty));
            _reportItem = new Forms.ToolStripMenuItem("", null, (s, e) => ReportRequested?.Invoke(this, EventArgs.Empty));
            _exitItem = new Forms.ToolStripMenuItem("", null, (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty));
            foreach (Forms.ToolStripItem item in new Forms.ToolStripItem[] { _openItem, _convertItem, _settingsItem, _updateItem, _reportItem, _exitItem })
                item.Padding = new Forms.Padding(4, 3, 4, 3);
            _menu.Items.AddRange(new Forms.ToolStripItem[]
            {
                _openItem, _convertItem, _settingsItem, new Forms.ToolStripSeparator(), _updateItem, _reportItem, new Forms.ToolStripSeparator(), _exitItem,
            });

            _icon = new Forms.NotifyIcon
            {
                Icon = LoadIcon(),
                ContextMenuStrip = _menu,
                Visible = true,
            };
            _icon.MouseClick += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left) OpenRequested?.Invoke(this, EventArgs.Empty);
            };
            ApplyLanguage();
        }

        /// <summary>Menu texts and direction in the interface language.</summary>
        public void ApplyLanguage()
        {
            _menu.RightToLeft = Loc.IsRightToLeft ? Forms.RightToLeft.Yes : Forms.RightToLeft.No;
            _openItem.Text = Loc.T("Tray.Open");
            _convertItem.Text = Loc.T("Tray.Convert");
            _settingsItem.Text = Loc.T("Nav.Settings");
            // The Store copy is updated by the Microsoft Store: say so instead of offering a check.
            _updateItem.Text = UpdateService.IsEnabled ? Loc.T("Common.CheckUpdate") : Loc.T("Update.FromStore");
            _updateItem.Enabled = UpdateService.IsEnabled;
            _reportItem.Text = Loc.T("Main.ReportTip");
            _exitItem.Text = Loc.T("Tray.Exit");
            var tip = Loc.T("App.Title");
            _icon.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip; // NotifyIcon limit
        }

        public event EventHandler OpenRequested;
        public event EventHandler ConvertRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler ExitRequested;
        public event EventHandler UpdateRequested;
        public event EventHandler ReportRequested;

        /// <summary>Shows the current hotkey next to «تحويل الحافظة» (convert the clipboard).</summary>
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
