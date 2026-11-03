using System.Collections.Generic;
using System.Runtime.Serialization;

namespace InstaWords.App.Models
{
    /// <summary>Everything the app remembers, saved as settings.json in %APPDATA%\FastKiwi\InstaWords.</summary>
    [DataContract(Name = "settings", Namespace = "")]
    public sealed class AppSettings
    {
        public const int CurrentSchema = 1;

        public AppSettings() => SetDefaults();

        [OnDeserializing]
        private void OnDeserializing(StreamingContext _) => SetDefaults();

        private void SetDefaults()
        {
            Schema = CurrentSchema;
            Language = "ar";
            Unit = "SAR";
            HundredsForm = "مائة";
            Decimals = "round";
            Prefix = "";
            Suffix = "";
            Hotkey = "Ctrl+Alt+T";
            HotkeyEnabled = true;
            CloseToTray = true;
            MinimizeToTray = false;
            ShowToast = true;
            OptionsExpanded = false;
            WindowWidth = 1080;
            WindowHeight = 760;
            WindowLeft = null;
            WindowTop = null;
        }

        [DataMember(Name = "schema", Order = 0)] public int Schema { get; set; }

        // Conversion defaults: used by the window, the bulk tab and the Ctrl+Alt+T hotkey.
        [DataMember(Name = "language", Order = 1)] public string Language { get; set; }
        [DataMember(Name = "unit", Order = 2)] public string Unit { get; set; }
        [DataMember(Name = "hundredsForm", Order = 3)] public string HundredsForm { get; set; }
        [DataMember(Name = "decimals", Order = 4)] public string Decimals { get; set; }
        [DataMember(Name = "prefix", Order = 5)] public string Prefix { get; set; }
        [DataMember(Name = "suffix", Order = 6)] public string Suffix { get; set; }

        [DataMember(Name = "hotkey", Order = 10)] public string Hotkey { get; set; }
        [DataMember(Name = "hotkeyEnabled", Order = 11)] public bool HotkeyEnabled { get; set; }
        [DataMember(Name = "closeToTray", Order = 12)] public bool CloseToTray { get; set; }
        [DataMember(Name = "minimizeToTray", Order = 13)] public bool MinimizeToTray { get; set; }
        [DataMember(Name = "showToast", Order = 14)] public bool ShowToast { get; set; }
        [DataMember(Name = "trayHintShown", Order = 15)] public bool TrayHintShown { get; set; }

        [DataMember(Name = "optionsExpanded", Order = 20)] public bool OptionsExpanded { get; set; }
        [DataMember(Name = "windowLeft", Order = 21)] public double? WindowLeft { get; set; }
        [DataMember(Name = "windowTop", Order = 22)] public double? WindowTop { get; set; }
        [DataMember(Name = "windowWidth", Order = 23)] public double WindowWidth { get; set; }
        [DataMember(Name = "windowHeight", Order = 24)] public double WindowHeight { get; set; }
        [DataMember(Name = "windowMaximized", Order = 25)] public bool WindowMaximized { get; set; }
    }

    /// <summary>One remembered conversion (history.json).</summary>
    [DataContract(Name = "entry", Namespace = "")]
    public sealed class HistoryEntry
    {
        [DataMember(Name = "input", Order = 0)] public string Input { get; set; }
        [DataMember(Name = "cleaned", Order = 1)] public string Cleaned { get; set; }
        [DataMember(Name = "unit", Order = 2)] public string Unit { get; set; }
        [DataMember(Name = "language", Order = 3)] public string Language { get; set; }
        [DataMember(Name = "text", Order = 4)] public string Text { get; set; }
        /// <summary>ISO 8601, local time with offset.</summary>
        [DataMember(Name = "time", Order = 5)] public string Time { get; set; }

        // Display helpers (not serialized).
        public string Meta { get; set; }
        public System.Windows.FlowDirection TextFlow => Catalog.FlowFor(Language);
    }

    [DataContract(Name = "history", Namespace = "")]
    public sealed class HistoryFile
    {
        [DataMember(Name = "schema", Order = 0)] public int Schema { get; set; } = 1;
        [DataMember(Name = "entries", Order = 1)] public List<HistoryEntry> Entries { get; set; } = new List<HistoryEntry>();
    }
}
