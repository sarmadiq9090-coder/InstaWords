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
    /// «عدة مبالغ» (Bulk): a native DataGrid of numbers. Conversion runs off the UI thread and the grid is
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
            ApplyHeaders();
            Loc.Changed += (s, e) =>
            {
                ApplyHeaders();
                OnGridSelectionChanged(null, null);
                ConvertAll(); // error messages follow the interface language
                if (_status != null) SetStatus(_status, _statusOk); // re-worded by its function
            };
        }

        /// <summary>Column headers (DataGrid columns are not in the visual tree, so no DynamicResource there).</summary>
        private void ApplyHeaders()
        {
            ColInput.Header = Loc.T("Bulk.ColInput");
            ColCleaned.Header = Loc.T("Bulk.ColCleaned");
            ColText.Header = Loc.T("Bulk.ColText");
            ColError.Header = Loc.T("Bulk.ColError");
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

        /// <summary>Replaces the rows; <paramref name="statusKey"/> is the Strings key of the status message.</summary>
        public void LoadValues(IEnumerable<string> values, string statusKey, params object[] args)
        {
            SetRows(values.Select(v => new BulkRow(v)).ToList());
            SetStatus(statusKey, true, args);
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
                var previous = _status;
                var previousOk = _statusOk;
                var count = rows.Length;
                Func<string> progress = () => Loc.F("Bulk.Converting", Conversion.FormatCount(count));
                SetStatus(progress, ok: true);
                results = await Task.Run(() => inputs.AsParallel().AsOrdered().Select(i => Conversion.Run(i, options)).ToArray());
                if (version != _version) return; // options or rows changed meanwhile
                if (_status == progress) SetStatus(previous, previousOk);
            }
            for (var i = 0; i < rows.Length; i++)
            {
                var r = results[i];
                rows[i].SetResult(r.Ok, r.Amount ?? "", r.Ok ? r.Text : "", r.Ok ? r.Warning ?? "" : r.ErrorMessage, flow);
            }
            UpdateCounts();
        }

        private void OnRowEdited(BulkRow row)
        {
            var options = App.Current.State.ToOptions();
            var r = Conversion.Run(row.Input, options);
            row.SetResult(r.Ok, r.Amount ?? "", r.Ok ? r.Text : "", r.Ok ? r.Warning ?? "" : r.ErrorMessage, Catalog.FlowFor(options.Language));
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
            PrintRowsButton.IsEnabled = ok > 0;
        }

        private Func<string> _status;
        private bool _statusOk = true;

        /// <summary>The status line; kept as a function so it is re-worded when the interface language changes.</summary>
        private void SetStatus(Func<string> text, bool ok)
        {
            _status = text;
            _statusOk = ok;
            Status.Text = text?.Invoke() ?? "";
            Status.Foreground = (Brush)FindResource(ok ? "AccentTextBrush" : "DangerBrush");
        }

        private void SetStatus(string key, bool ok, params object[] args) => SetStatus(() => Loc.F(key, args), ok);

        // ---------- Print ----------

        /// <summary>The rows to print: the selected ones, or every row when none is selected (errors skipped).</summary>
        public IList<string> RowsToPrint()
        {
            var selected = Grid.SelectedItems.OfType<BulkRow>().ToList();
            var source = selected.Count > 0 ? selected.OrderBy(r => r.Index) : (IEnumerable<BulkRow>)_rows;
            return source.Where(r => r.Ok).Select(r => r.Input).ToList();
        }

        /// <summary>«طباعة» on this tab (and Ctrl+P): one page per row.</summary>
        public void PrintRows()
        {
            var rows = RowsToPrint();
            if (rows.Count == 0)
            {
                SetStatus("Bulk.PrintNothing", ok: false);
                return;
            }
            App.Current.AppWindow?.ShowPrint(rows);
        }

        private void OnPrintClick(object sender, RoutedEventArgs e) => PrintRows();

        private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // «طباعة 3 صفوف» when several rows are selected, plain «طباعة» otherwise.
            var n = Grid.SelectedItems.OfType<BulkRow>().Count(r => r.Ok);
            if (n > 1) PrintRowsButton.Content = Loc.F("Bulk.PrintSelected", Conversion.FormatCount(n));
            else PrintRowsButton.SetResourceReference(ContentProperty, "Bulk.Print");
        }

        // ---------- Paste / import ----------

        public void PasteFromClipboard()
        {
            if (!ClipboardService.TryGetText(out var text) || string.IsNullOrWhiteSpace(text))
            {
                SetStatus("Bulk.ClipboardEmpty", ok: false);
                return;
            }
            var values = Csv.PastedValues(text);
            // A pasted header ("المبلغ") is skipped like in CSV files.
            if (values.Count > 1 && !values[0].Any(Csv.IsDigit)) values.RemoveAt(0);
            if (RefuseTooMany(values.Count)) return;
            LoadValues(values, "Bulk.Pasted", Conversion.FormatCount(values.Count));
        }

        public void ImportFile(string path)
        {
            try
            {
                // Checked before reading: a workbook or a binary file would be read as garbage, a huge one would fill the memory.
                var name = Path.GetFileName(path);
                var size = new FileInfo(path).Length;
                switch (Csv.Inspect(path, size, Csv.ReadHead(path, Csv.HeadBytes)))
                {
                    case Csv.ImportProblem.Spreadsheet:
                        Refuse("Bulk.SpreadsheetFile", name);
                        return;
                    case Csv.ImportProblem.TooLarge:
                        Refuse("Bulk.FileTooLarge", name, Csv.Megabytes(size), Csv.Megabytes(Csv.MaxImportBytes));
                        return;
                    case Csv.ImportProblem.NotText:
                        Refuse("Bulk.NotTextFile", name);
                        return;
                }
                var values = Csv.ColumnValues(Csv.ReadText(path));
                if (RefuseTooMany(values.Count)) return;
                LoadValues(values, "Bulk.Imported", Conversion.FormatCount(values.Count), name);
            }
            catch (Exception ex)
            {
                SetStatus("Bulk.ImportFailed", false, ex.Message);
            }
        }

        /// <summary>More than <see cref="Csv.MaxImportRows"/> numbers: nothing is loaded (never a silent cut), and the user is told.</summary>
        private bool RefuseTooMany(int count)
        {
            if (count <= Csv.MaxImportRows) return false;
            Refuse("Bulk.TooManyRows", Conversion.FormatCount(count), Conversion.FormatCount(Csv.MaxImportRows));
            return true;
        }

        /// <summary>A file or list that was not loaded: the reason on the status line, and in full in a notice.</summary>
        private void Refuse(string key, params object[] args)
        {
            SetStatus(key, false, args);
            ToastWindow.ShowMessage(false, Loc.T("Bulk.NotLoadedTitle"), Loc.F(key, args));
        }

        private void OnPasteClick(object sender, RoutedEventArgs e) => PasteFromClipboard();

        private void OnImportClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = Loc.T("Bulk.ImportTitle"),
                Filter = Loc.T("Bulk.ImportFilter") + " (*.csv;*.txt;*.tsv)|*.csv;*.txt;*.tsv|" + Loc.T("Bulk.AllFiles") + " (*.*)|*.*",
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
            {
                var values = Csv.PastedValues(text);
                if (!RefuseTooMany(values.Count)) LoadValues(values, "Bulk.Dropped");
            }
        }

        private void OnSampleClick(object sender, RoutedEventArgs e) => LoadValues(Sample, "Bulk.SampleLoaded");

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            SetRows(new List<BulkRow>());
            SetStatus((Func<string>)null, ok: true);
        }

        // ---------- Copy / export ----------

        private void OnCopyAllClick(object sender, RoutedEventArgs e)
        {
            // One line per input row; rows with an error stay empty so the lines match the pasted column.
            var text = string.Join("\r\n", _rows.Select(r => r.Ok ? r.Text : ""));
            var errs = _rows.Count(r => !r.Ok);
            var lines = Conversion.FormatCount(_rows.Count);
            if (ClipboardService.TrySetText(text))
            {
                if (errs > 0) SetStatus("Bulk.CopiedWithErrors", true, lines, Conversion.FormatCount(errs));
                else SetStatus("Bulk.Copied", true, lines);
            }
            else SetStatus("Bulk.CopyFailed", ok: false);
        }

        private void OnExportClick(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = Loc.T("Bulk.ExportTitle"),
                FileName = "tafqit.csv",
                DefaultExt = ".csv",
                Filter = Loc.T("Bulk.ExportFilter") + " (*.csv)|*.csv",
                AddExtension = true,
                OverwritePrompt = true,
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
            try
            {
                ExportTo(dlg.FileName);
                SetStatus("Bulk.Exported", true, Conversion.FormatCount(_rows.Count), Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                SetStatus("Bulk.ExportFailed", false, ex.Message);
            }
        }

        public void ExportTo(string path)
        {
            var rows = new List<IEnumerable<string>> { new[] { Loc.T("Bulk.CsvNumber"), Loc.T("Bulk.CsvCleaned"), Loc.T("Bulk.CsvWords"), Loc.T("Bulk.CsvError") } };
            rows.AddRange(_rows.Select(r => (IEnumerable<string>)new[]
            {
                Csv.SafeCell(r.Input), Csv.SafeCell(r.Cleaned ?? ""), Csv.SafeCell(r.Ok ? r.Text : ""), Csv.SafeCell(r.Error ?? ""), // a warning on an ok row, the error otherwise
            }));
            Csv.Save(path, rows);
        }
    }
}
