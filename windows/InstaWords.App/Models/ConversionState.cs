using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using InstaWords.Core;

namespace InstaWords.App.Models
{
    /// <summary>
    /// The conversion options shared by the single tab, the bulk tab, the settings tab and the
    /// clipboard hotkey. They are the user's defaults and are saved as soon as they change.
    /// </summary>
    public sealed class ConversionState : INotifyPropertyChanged
    {
        private string _language = "ar";
        private string _unit = "SAR";
        private string _hundredsForm = "مائة";
        private string _decimals = "round";
        private string _prefix = "";
        private string _suffix = "";

        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>Raised once per change of any option (after PropertyChanged).</summary>
        public event EventHandler Changed;

        public string Language
        {
            get => _language;
            set
            {
                var lang = Catalog.FindLanguage(value)?.Id ?? "ar";
                if (lang == _language) return;
                // Swap a ready-made prefix/suffix for the same preset in the new language.
                var old = Presets.For(_language);
                if ((old.Prefix.Length > 0 || old.Suffix.Length > 0) && Prefix.Trim() == old.Prefix && Suffix.Trim() == old.Suffix)
                {
                    var next = Presets.For(lang);
                    _prefix = next.Prefix;
                    _suffix = next.Suffix;
                    Raise(nameof(Prefix));
                    Raise(nameof(Suffix));
                }
                _language = lang;
                Raise();
                Raise(nameof(LanguageItem));
                Raise(nameof(ResultFlow));
                Raise(nameof(SupportsHundredsForm));
                Raise(nameof(PresetLabel));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public string Unit
        {
            get => _unit;
            set
            {
                var unit = Catalog.FindUnit(value)?.Id ?? "none";
                if (unit == _unit) return;
                _unit = unit;
                Raise();
                Raise(nameof(UnitItem));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public string HundredsForm { get => _hundredsForm; set => Set(ref _hundredsForm, value == "مئة" ? "مئة" : "مائة"); }
        public string Decimals { get => _decimals; set => Set(ref _decimals, value == "error" ? "error" : "round"); }
        public string Prefix { get => _prefix; set => Set(ref _prefix, value ?? ""); }
        public string Suffix { get => _suffix; set => Set(ref _suffix, value ?? ""); }

        public LanguageItem LanguageItem
        {
            get => Catalog.FindLanguage(_language);
            set { if (value != null) Language = value.Id; }
        }

        public UnitItem UnitItem
        {
            get => Catalog.FindUnit(_unit);
            set { if (value != null) Unit = value.Id; }
        }

        public FlowDirection ResultFlow => Catalog.FlowFor(_language);
        public bool SupportsHundredsForm => Catalog.SupportsHundredsForm(_language);
        public string PresetLabel => Presets.For(_language).Label;

        public void ApplyPreset()
        {
            var p = Presets.For(_language);
            Prefix = p.Prefix;
            Suffix = p.Suffix;
        }

        public void ClearAffixes()
        {
            Prefix = "";
            Suffix = "";
        }

        public TafqitOptions ToOptions() => new TafqitOptions
        {
            Language = _language,
            Unit = _unit,
            HundredsForm = _hundredsForm,
            Decimals = _decimals,
            Prefix = _prefix.Trim(),
            Suffix = _suffix.Trim(),
        };

        public void Load(AppSettings s)
        {
            _language = Catalog.FindLanguage(s.Language)?.Id ?? "ar";
            _unit = Catalog.FindUnit(s.Unit)?.Id ?? Catalog.FindUnit("SAR")?.Id ?? "none";
            _hundredsForm = s.HundredsForm == "مئة" ? "مئة" : "مائة";
            _decimals = s.Decimals == "error" ? "error" : "round";
            _prefix = s.Prefix ?? "";
            _suffix = s.Suffix ?? "";
            Raise(string.Empty);
        }

        public void SaveTo(AppSettings s)
        {
            s.Language = _language;
            s.Unit = _unit;
            s.HundredsForm = _hundredsForm;
            s.Decimals = _decimals;
            s.Prefix = _prefix;
            s.Suffix = _suffix;
        }

        private void Set(ref string field, string value, [CallerMemberName] string name = null)
        {
            if (field == value) return;
            field = value;
            Raise(name);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Raise([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
