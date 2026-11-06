using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using InstaWords.App.Services;
using Microsoft.Win32;

namespace InstaWords.App.Views
{
    /// <summary>Company name and logo printed at the top of receipt and payment vouchers.</summary>
    public partial class VoucherHeaderEditor : UserControl
    {
        /// <summary>Raised by any copy of the editor (Settings, print dialog) after a change.</summary>
        public static event EventHandler HeaderChanged;

        private bool _syncing;

        public VoucherHeaderEditor()
        {
            InitializeComponent();
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) return;
            Loaded += (s, e) => { HeaderChanged -= OnOtherChanged; HeaderChanged += OnOtherChanged; Sync(); };
            Unloaded += (s, e) => HeaderChanged -= OnOtherChanged;
            SizeChanged += (s, e) => ApplyLayout(e.NewSize.Width);
        }

        /// <summary>Below this width the company name would be squeezed (its label cut), so the logo goes under it.</summary>
        public const double StackBelowWidth = 420;

        private void ApplyLayout(double width)
        {
            var stacked = width < StackBelowWidth;
            Grid.SetRow(LogoPanel, stacked ? 1 : 0);
            Grid.SetColumn(LogoPanel, stacked ? 0 : 2);
            Grid.SetColumnSpan(CompanyPanel, stacked ? 3 : 1);
            LogoPanel.Margin = new Thickness(0, stacked ? 10 : 0, 0, 0);
            GapColumn.Width = new GridLength(stacked ? 0 : 16);
        }

        private static PrintSettings Print => App.Current.Settings.Print;

        private void OnOtherChanged(object sender, EventArgs e)
        {
            if (!ReferenceEquals(sender, this)) Sync();
        }

        private void Sync()
        {
            _syncing = true;
            if (CompanyBox.Text != (Print.CompanyName ?? "")) CompanyBox.Text = Print.CompanyName ?? "";
            _syncing = false;
            var logo = PrintRenderer.LoadLogo(Print.LogoPath);
            LogoImage.Source = logo;
            LogoPlaceholder.Visibility = logo == null ? Visibility.Visible : Visibility.Collapsed;
            RemoveLogo.Visibility = logo == null ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnCompanyChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            Print.CompanyName = CompanyBox.Text;
            App.Current.SaveSettingsSoon();
            HeaderChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnChooseLogo(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = Loc.T("Print.LogoChoose"),
                Filter = Loc.T("Print.LogoFilter") + " (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
            if (PrintRenderer.LoadLogo(dlg.FileName) == null)
            {
                ToastWindow.ShowMessage(false, Loc.T("Print.LogoFailed"), Path.GetFileName(dlg.FileName));
                return;
            }
            try
            {
                // Keep a copy next to the settings, so moving or deleting the original does not break vouchers.
                Directory.CreateDirectory(AppPaths.DataDir);
                var target = Path.Combine(AppPaths.DataDir, "logo" + Path.GetExtension(dlg.FileName).ToLowerInvariant());
                DeleteOldLogo(except: target);
                File.Copy(dlg.FileName, target, overwrite: true);
                Print.LogoPath = target;
            }
            catch (Exception ex)
            {
                Log.Warn("Logo copy: " + ex.Message);
                Print.LogoPath = dlg.FileName;
            }
            App.Current.SaveSettingsSoon();
            Sync();
            HeaderChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnRemoveLogo(object sender, RoutedEventArgs e)
        {
            DeleteOldLogo(except: null);
            Print.LogoPath = null;
            App.Current.SaveSettingsSoon();
            Sync();
            HeaderChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Removes our own copy (never a file elsewhere on the disk).</summary>
        private static void DeleteOldLogo(string except)
        {
            var old = Print.LogoPath;
            if (string.IsNullOrEmpty(old) || string.Equals(old, except, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                if (string.Equals(Path.GetDirectoryName(Path.GetFullPath(old)), Path.GetFullPath(AppPaths.DataDir), StringComparison.OrdinalIgnoreCase) && File.Exists(old))
                    File.Delete(old);
            }
            catch (Exception ex)
            {
                Log.Warn("Logo delete: " + ex.Message);
            }
        }
    }
}
