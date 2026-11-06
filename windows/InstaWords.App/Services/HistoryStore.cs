using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Threading;
using InstaWords.App.Models;

namespace InstaWords.App.Services
{
    /// <summary>Recent conversions (newest first), saved as history.json.</summary>
    public sealed class HistoryStore
    {
        public const int MaxEntries = 100;
        private readonly DispatcherTimer _saveTimer;

        public HistoryStore()
        {
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); Save(); };
        }

        public ObservableCollection<HistoryEntry> Items { get; } = new ObservableCollection<HistoryEntry>();

        public void Load()
        {
            var file = JsonStore.Load<HistoryFile>(AppPaths.HistoryFile);
            Items.Clear();
            if (file?.Entries == null) return;
            foreach (var e in file.Entries.Where(e => e != null && !string.IsNullOrEmpty(e.Text)).Take(MaxEntries))
            {
                e.Meta = MetaFor(e);
                Items.Add(e);
            }
        }

        public void Add(string input, string cleaned, string unit, string language, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var existing = Items.FirstOrDefault(e => e.Cleaned == cleaned && e.Unit == unit && e.Language == language && e.Text == text);
            if (existing != null) Items.Remove(existing);
            var entry = new HistoryEntry
            {
                Input = (input ?? "").Trim(),
                Cleaned = cleaned,
                Unit = unit,
                Language = language,
                Text = text,
                Time = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            };
            entry.Meta = MetaFor(entry);
            Items.Insert(0, entry);
            while (Items.Count > MaxEntries) Items.RemoveAt(Items.Count - 1);
            ScheduleSave();
        }

        public void Clear()
        {
            Items.Clear();
            ScheduleSave();
        }

        public void ScheduleSave()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public void Save()
        {
            _saveTimer.Stop();
            JsonStore.Save(AppPaths.HistoryFile, new HistoryFile { Entries = Items.ToList() });
        }

        private static string MetaFor(HistoryEntry e)
        {
            var unit = Catalog.FindUnit(e.Unit);
            var unitText = unit == null ? e.Unit : (unit.Code ?? unit.Label);
            var lang = Catalog.FindLanguage(e.Language)?.Label ?? e.Language;
            // Entries are successful conversions: money as written in practice (45,750.00 SAR, 1,750,000 IQD), rounded like the words.
            var number = e.Cleaned != null ? AmountFormat.Display(e.Cleaned, e.Unit) : Conversion.Grouped(e.Input);
            return "\u200E" + number + "\u200E · " + unitText + " · " + lang;
        }
    }
}
