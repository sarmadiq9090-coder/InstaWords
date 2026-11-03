using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using InstaWords.App.Models;

namespace InstaWords.App.Views
{
    /// <summary>
    /// Unit ComboBox grouped by category. Typing (closed or open) filters by Arabic name, code,
    /// English or French name, or alias; Enter picks the first match, Down moves into the list.
    /// </summary>
    public partial class UnitPicker : UserControl
    {
        private readonly ListCollectionView _view;
        private TextBox _search;
        private ConversionState _state;
        private bool _filtering;

        public UnitPicker()
        {
            InitializeComponent();
            _view = new ListCollectionView(Catalog.Units.ToList());
            _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(UnitItem.CategoryLabel)));
            Combo.ItemsSource = _view;

            Combo.DropDownOpened += OnDropDownOpened;
            Combo.DropDownClosed += OnDropDownClosed;
            Combo.SelectionChanged += OnSelectionChanged;
            Combo.PreviewTextInput += OnComboTextInput;
            Combo.PreviewKeyDown += OnComboKeyDown;
            Combo.Loaded += (s, e) => AttachSearchBox();
            DataContextChanged += (s, e) => Bind(DataContext as ConversionState);
            Services.Loc.Changed += OnLanguageChanged;
            Unloaded += (s, e) => Services.Loc.Changed -= OnLanguageChanged;
            Loaded += (s, e) => { Services.Loc.Changed -= OnLanguageChanged; Services.Loc.Changed += OnLanguageChanged; };
        }

        /// <summary>Category headers are regrouped in the new language (unit names update by binding).</summary>
        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (Combo.IsDropDownOpen) Combo.IsDropDownOpen = false;
            _view.Refresh();
            SyncFromState();
        }

        public ComboBox ComboBox => Combo;

        private void Bind(ConversionState state)
        {
            if (_state != null) _state.PropertyChanged -= OnStateChanged;
            _state = state;
            if (_state != null) _state.PropertyChanged += OnStateChanged;
            SyncFromState();
        }

        private void OnStateChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ConversionState.Unit)) SyncFromState();
        }

        private void SyncFromState()
        {
            if (_state == null || _filtering) return;
            var item = _state.UnitItem;
            if (!ReferenceEquals(Combo.SelectedItem, item)) Combo.SelectedItem = item;
        }

        private void AttachSearchBox()
        {
            if (_search != null) return;
            Combo.ApplyTemplate();
            _search = Combo.Template.FindName("SearchBox", Combo) as TextBox;
            if (_search == null) return;
            _search.TextChanged += (s, e) => ApplyFilter(_search.Text);
            _search.PreviewKeyDown += OnSearchKeyDown;
        }

        private void OnComboTextInput(object sender, TextCompositionEventArgs e)
        {
            // Typing on the ComboBox goes to the search box: the first character opens the list,
            // and fast typists' next characters are not lost while the focus moves.
            if (string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;
            AttachSearchBox();
            if (_search == null || _search.IsKeyboardFocusWithin) return;
            e.Handled = true;
            if (!Combo.IsDropDownOpen)
            {
                _search.Text = e.Text;
                Combo.IsDropDownOpen = true;
            }
            else _search.Text += e.Text;
            _search.CaretIndex = _search.Text.Length;
        }

        private void OnComboKeyDown(object sender, KeyEventArgs e)
        {
            // Enter while filtering, with the focus not yet in the search box: take the first match.
            if (e.Key != Key.Enter || !Combo.IsDropDownOpen || !_filtering) return;
            if (e.OriginalSource is ComboBoxItem || _search == null || _search.IsKeyboardFocusWithin) return;
            OnSearchKeyDown(_search, e);
        }

        private void OnDropDownOpened(object sender, EventArgs e)
        {
            AttachSearchBox();
            // After the ComboBox has moved focus to its selected item, put it in the search box.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_search == null || !Combo.IsDropDownOpen) return;
                _search.Focus();
                Keyboard.Focus(_search);
                _search.CaretIndex = _search.Text.Length;
                if (Combo.SelectedItem != null && string.IsNullOrEmpty(_search.Text))
                    (Combo.ItemContainerGenerator.ContainerFromItem(Combo.SelectedItem) as FrameworkElement)?.BringIntoView();
            }), DispatcherPriority.Background);
        }

        private void OnDropDownClosed(object sender, EventArgs e)
        {
            if (_search != null && _search.Text.Length > 0) _search.Text = ""; // clears the filter
            _filtering = false;
            _view.Filter = null;
            SyncFromState();
        }

        private void ApplyFilter(string query)
        {
            var terms = Catalog.SearchTerms(query);
            _filtering = terms.Length > 0;
            _view.Filter = terms.Length == 0 ? (Predicate<object>)null : o => ((UnitItem)o).Matches(terms);
            if (!_filtering) SyncFromState();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // While filtering, the current unit may be hidden; that is not a choice.
            if (Combo.SelectedItem is UnitItem u && _state != null) _state.Unit = u.Id;
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                {
                    // No search text: Enter just closes the list and keeps the current unit.
                    var first = _filtering ? _view.Cast<UnitItem>().FirstOrDefault() : null;
                    if (first != null && _state != null)
                    {
                        _filtering = false;
                        _state.Unit = first.Id;
                    }
                    Combo.IsDropDownOpen = false;
                    Combo.Focus();
                    e.Handled = true;
                    break;
                }
                case Key.Down:
                {
                    var target = (Combo.SelectedItem != null && !_filtering ? Combo.SelectedItem : _view.Cast<object>().FirstOrDefault());
                    if (target != null && Combo.ItemContainerGenerator.ContainerFromItem(target) is ComboBoxItem c)
                    {
                        c.Focus();
                        e.Handled = true;
                    }
                    break;
                }
                case Key.Escape:
                    Combo.IsDropDownOpen = false;
                    Combo.Focus();
                    e.Handled = true;
                    break;
            }
        }
    }
}
