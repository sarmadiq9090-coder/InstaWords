using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using InstaWords.App.Services;

namespace InstaWords.App.Views
{
    /// <summary>
    /// «المزيد من FastKiwi» / "More from FastKiwi": one line per other FastKiwi app (name, tagline, page
    /// link, and a Microsoft Store link when its listing is live) in the interface language.
    /// </summary>
    public partial class MoreAppsPanel : UserControl
    {
        public MoreAppsPanel()
        {
            InitializeComponent();
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) return;
            Loaded += (s, e) => { Loc.Changed += OnLanguageChanged; Build(); };
            Unloaded += (s, e) => Loc.Changed -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object sender, EventArgs e) => Build();

        private void Build()
        {
            List.Children.Clear();
            foreach (var app in MoreApps.LoadEmbedded(Loc.Language))
            {
                var line = new TextBlock { FontSize = 13.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
                line.Inlines.Add(Link(new Run(app.Name) { FontWeight = FontWeights.SemiBold }, app.PageUrl, "MoreApps." + app.Id));
                if (app.Tagline.Length > 0) line.Inlines.Add(Muted(" · " + app.Tagline));
                if (app.ComingSoon) line.Inlines.Add(Muted(" · " + Loc.T("More.ComingSoon")));
                else if (app.StoreUri != null)
                {
                    line.Inlines.Add(Muted(" · "));
                    line.Inlines.Add(Link(new Run(Loc.T("More.Store")), app.StoreUri, "MoreApps." + app.Id + ".Store"));
                }
                List.Children.Add(line);
                if (app.Description.Length > 0)
                    List.Children.Add(new TextBlock { Style = (Style)FindResource("HintText"), Text = app.Description, Margin = new Thickness(0, 2, 0, 0) });
            }
        }

        private Run Muted(string text) => new Run(text) { Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush") };

        private static Hyperlink Link(Inline content, string url, string automationId)
        {
            var link = new Hyperlink(content) { Tag = url, ToolTip = url };
            System.Windows.Automation.AutomationProperties.SetAutomationId(link, automationId);
            link.Click += (s, e) => Links.OpenListed((string)((Hyperlink)s).Tag);
            return link;
        }
    }
}
