using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using InstaWords.App.Models;
using InstaWords.App.Services;
using Microsoft.Win32;

namespace InstaWords.App.Views
{
    /// <summary>
    /// «دفعة»: a native DataGrid of numbers. Conversion runs off the UI thread and the grid is
    /// virtualized, so 10,000+ rows stay smooth.
    /// </summary>
    public partial class BulkPage : UserControl
    {
        private static readonly string[] Sample = { "1250.75", "٣٠٠٠", "15,000", "0.5", "2000000", "101", "abc" };
        private ObservableCollection<BulkRow> _rows = new ObservableCollection<BulkRow>();
        private int _version;

        public BulkPage()
        {
            InitializeComponent();
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) return;
            DataContext = App.Current.State;
            App.Current.State.Changed += (s, e) => ConvertAll();
            SetRows(new List<BulkRow>());
            Drop += OnDrop;
            DragOver += OnDragOver;
        }

        public IReadOnlyList<BulkRow> Rows => _rows;

        public void FocusGrid() => Grid.Focus();

        private void SetRows(List<BulkRow> rows)
        {
            if (_rows != null) _rows.CollectionChanged -= OnRowsChanged;
            foreach (var r in rows) r.InputEdited += OnRowEdited;
            for (var i = 0; i < rows.Count; i++) rows[i].Index = i + 1;
            _rows = new ObservableCollection<BulkRow>(rows);
            _rows.CollectionChanged += OnRowsChanged;
            Grid.ItemsSource = _rows; // one reset instead of thousands of add notifications
            EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ConvertAll();
        }

        private void OnRowsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Rows deleted with the Delete key: renumber and recount.
            for (var i = 0; i < _rows.Count; i++) _rows[i].Index = i + 1;
            EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateCounts();
        }

        public void LoadValues(IEnumerable<string> values, string status)
        {
            SetRows(values.Select(v => new BulkRow(v)).ToList());
            SetStatus(status, ok: true);
        }

        /// <summary>Converts every row on a worker thread, then applies the results in one pass.</summary>
        public async void ConvertAll()
        {
            var version = Interlocked.Increment(ref _version);
            var rows = _rows.ToArray();
            var options = App.Current.State.ToOptions();
            var flow = Catalog.FlowFor(options.Language);
            if (rows.Length == 0) { UpdateCounts(); return; }

            var inputs = rows.Select(r => r.Input).ToArray();
            ConversionResult[] results;
            if (rows.Length <= 300) results = inputs.Select(i => Conversion.Run(i, options)).ToArray();
            else
            {
                var previous = Status.Text;
                var progress = "جارٍ التحويل… " + Conversion.FormatCount(rows.Length) + " صفاً";
                SetStatus(progress, ok: true);
                results = await Task.Run(() => inputs.AsParallel().AsOrdered().Select(i => Conversion.Run(i, options)).ToArray());
                if (version != _version) return; // options or rows changed meanwhile
                if (Status.Text == progress) SetStatus(previous, ok: true);
            }
            for (var i = 0; i < rows.Length; i++)
            {
                var r = results[i];
                rows[i].SetResult(r.Ok, r.Cleaned ?? "", r.Ok ? r.Text : "", r.Ok ? "" : r.ErrorMessage, flow);
            }
            UpdateCounts();
        }

        private void OnRowEdited(BulkRow row)
        {
            var options = App.Current.State.ToOptions();
            var r = Conversion.Run(row.Input, options);
            row.SetResult(r.Ok, r.Cleaned ?? "", r.Ok ? r.Text : "", r.Ok ? "" : r.ErrorMessage, Catalog.FlowFor(options.Language));
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            var all = _rows.Count;
            var ok = _rows.Count(r => r.Ok);
            CountAll.Text = Conversion.FormatCount(all);
            CountOk.Text = Conversion.FormatCount(ok);
            CountErr.Text = Conversion.FormatCount(all - ok);
            CopyAllButton.IsEnabled = ok > 0;
            ExportButton.IsEnabled = all > 0;
        }

        private void SetStatus(string text, bool ok)
        {
            Status.Text = text ?? "";
            Status.Foreground = (Brush)FindResource(ok ? "AccentTextBrush" : "DangerBrush");
        }

        // ---------- Paste / import ----------

        public void PasteFromClipboard()
        {
            if (!ClipboardService.TryGetText(out var text) || string.IsNullOrWhiteSpace(text))
            {
                SetStatus("الحافظة لا تحتوي على نص. انسخ عموداً من Excel ثم اضغط Ctrl+V.", ok: false);
                return;
            }
            var values = Csv.PastedValues(text);
            // A pasted header ("المبلغ") is skipped like in CSV files.
            if (values.Count > 1 && !values[0].Any(Csv.IsDigit)) values.RemoveAt(0);
            LoadValues(values, "لُصق " + Conversion.FormatCount(values.Count) + " رقماً من الحافظة.");
        }

        public void ImportFile(string path)
        {
            try
            {
                var values = Csv.ColumnValues(Csv.ReadText(path));
                LoadValues(values, "استُورد " + Conversion.FormatCount(values.Count) + " رقماً من الملف «" + Path.GetFileName(path) + "».");
            }
            catch (Exception ex)
            {
                SetStatus("تعذّرت قراءة الملف. تأكد أنه ملف CSV أو نصي. (" + ex.Message + ")", ok: false);
            }
        }

        private void OnPasteClick(object sender, RoutedEventArgs e) => PasteFromClipboard();

        private void OnImportClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "استيراد أرقام",
                Filter = "ملفات CSV أو نصية (*.csv;*.txt;*.tsv)|*.csv;*.txt;*.tsv|كل الملفات (*.*)|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) == true) ImportFile(dlg.FileName);
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText)
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) ImportFile(files[0]);
            else if (e.Data.GetData(DataFormats.UnicodeText) is string text)
                LoadValues(Csv.PastedValues(text), "أُضيفت الأرقام المسحوبة.");
        }

        private void OnSampleClick(object sender, RoutedEventArgs e) => LoadValues(Sample, "مثال: سبعة أرقام، منها سطر خاطئ عمداً.");

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            SetRows(new List<BulkRow>());
            SetStatus("", ok: true);
        }

        // ---------- Copy / export ----------

        private void OnCopyAllClick(object sender, RoutedEventArgs e)
        {
            // One line per input row; rows with an error stay empty so the lines match the pasted column.
            var text = string.Join("\r\n", _rows.Select(r => r.Ok ? r.Text : ""));
            var errs = _rows.Count(r => !r.Ok);
            if (ClipboardService.TrySetText(text))
                SetStatus("نُسخ " + Conversion.FormatCount(_rows.Count) + " سطراً" +
                          (errs > 0 ? "، وتُركت أسطر الأخطاء (" + Conversion.FormatCount(errs) + ") فارغة." : "."), ok: true);
            else SetStatus("تعذّر النسخ.", ok: false);
        }

        private void OnExportClick(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "تصدير النتائج",
                FileName = "tafqit.csv",
                DefaultExt = ".csv",
                Filter = "CSV بترميز UTF-8 (*.csv)|*.csv",
                AddExtension = true,
                OverwritePrompt = true,
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
            try
            {
                ExportTo(dlg.FileName);
                SetStatus("صُدّر " + Conversion.FormatCount(_rows.Count) + " صفاً إلى «" + Path.GetFileName(dlg.FileName) + "».", ok: true);
            }
            catch (Exception ex)
            {
                SetStatus("تعذّر حفظ الملف: " + ex.Message, ok: false);
            }
        }

        public void ExportTo(string path)
        {
            var rows = new List<IEnumerable<string>> { new[] { "الرقم", "الرقم بعد التنقية", "التفقيط", "الخطأ" } };
            rows.AddRange(_rows.Select(r => (IEnumerable<string>)new[]
            {
                Csv.SafeCell(r.Input), Csv.SafeCell(r.Cleaned ?? ""), Csv.SafeCell(r.Ok ? r.Text : ""), Csv.SafeCell(r.Ok ? "" : r.Error ?? ""),
            }));
            Csv.Save(path, rows);
        }
    }
}
