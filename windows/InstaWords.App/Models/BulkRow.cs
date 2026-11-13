using System.ComponentModel;
using System.Windows;

namespace InstaWords.App.Models
{
    /// <summary>One line of the bulk table.</summary>
    public sealed class BulkRow : INotifyPropertyChanged
    {
        private int _index;
        private string _input;
        private string _cleaned;
        private string _text;
        private string _error;
        private bool _ok;
        private FlowDirection _textFlow = FlowDirection.RightToLeft;

        public BulkRow(string input) => _input = input ?? "";

        public event PropertyChangedEventHandler PropertyChanged;

        public int Index { get => _index; set { if (_index != value) { _index = value; Raise(nameof(Index)); } } }

        /// <summary>The value as entered; editable in the grid (the row is re-converted).</summary>
        public string Input
        {
            get => _input;
            set
            {
                value = (value ?? "").Trim();
                if (_input == value) return;
                _input = value;
                Raise(nameof(Input));
                InputEdited?.Invoke(this);
            }
        }

        /// <summary>The amount used (ConversionResult.Amount: 45750.50 SAR), or the cleaned input of a failed row; also the CSV «cleaned» column.</summary>
        public string Cleaned => _cleaned;
        public string Text => _text;
        /// <summary>The error of a failed row, or the warning of a converted one (ROUNDED_TO_ZERO); "" when none.</summary>
        public string Error => _error;
        public bool Ok => _ok;
        public FlowDirection TextFlow => _textFlow;

        internal event System.Action<BulkRow> InputEdited;

        /// <summary>Stores a conversion result; raises change notifications only for what changed.</summary>
        public void SetResult(bool ok, string cleaned, string text, string error, FlowDirection flow)
        {
            if (_ok != ok) { _ok = ok; Raise(nameof(Ok)); }
            if (_cleaned != cleaned) { _cleaned = cleaned; Raise(nameof(Cleaned)); }
            if (_text != text) { _text = text; Raise(nameof(Text)); }
            if (_error != error) { _error = error; Raise(nameof(Error)); }
            if (_textFlow != flow) { _textFlow = flow; Raise(nameof(TextFlow)); }
        }

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
