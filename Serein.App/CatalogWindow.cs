using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Serein.Core;

namespace Serein.App;
public sealed class CatalogWindow : Window
{
    readonly AutoEqCatalog catalog;
    readonly System.Windows.Controls.TextBox search = new() { Margin = new(0,15,0,12), ToolTip = T("Kopfhörer suchen, z. B. Ananda oder DT 880") };
    readonly ListBox results = new();
    readonly TextBlock status = new() { Margin = new(0,10,0,0), TextWrapping = TextWrapping.Wrap };
    readonly System.Windows.Controls.Button download = new() { Content = T("Preset laden und ansehen") };
    readonly System.Windows.Controls.Button refresh = new() { Content = T("Katalog aktualisieren") };
    readonly TextBlock infoTitle = new() { Text = T("Ein Preset auswählen"), FontSize = 20, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock infoMeta = new() { Margin = new(0,12,0,16), TextWrapping = TextWrapping.Wrap };
    readonly TextBlock infoNotes = new() { Text = T("Hier erscheinen Messquelle, Kategorie und veröffentlichte Hinweise."), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    readonly System.Windows.Controls.Button source = new() { Content = T("Originalquelle öffnen ↗"), Margin = new(0,12,0,0), IsEnabled = false };
    readonly Dictionary<string, CatalogInfo> infoCache = [];
    int infoRevision;
    bool closed;
    public bool ProfileLoaded { get; private set; }
    public CatalogWindow(string dataDirectory, Func<Profile, bool> preview)
    {
        DarkChrome.Attach(this); Style = (Style)FindResource(typeof(Window)); Title = T("Serein · Kopfhörer-Presets"); Width = 1060; Height = 720; MinWidth = 850; MinHeight = 580; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        catalog = new(Path.Combine(dataDirectory, "autoeq")); catalog.Load(Path.Combine(AppContext.BaseDirectory, "autoeq-index.md"));
        var panel = new DockPanel { Margin = new(26) }; panel.SetResourceReference(Panel.BackgroundProperty, "Bg"); Content = panel;
        var header = new StackPanel(); header.Children.Add(new TextBlock { Text = T("Kopfhörer-Presets"), FontSize = 26 }); header.Children.Add(new TextBlock { Text = T("AutoEQ-Katalog · Messquellen und Preset-Informationen"), Margin = new(0,8,0,0) }); header.Children.Add(search); DockPanel.SetDock(header, Dock.Top); panel.Children.Add(header);
        var footer = new StackPanel(); var actions = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal }; actions.Children.Add(download); actions.Children.Add(refresh); footer.Children.Add(actions); footer.Children.Add(status); DockPanel.SetDock(footer, Dock.Bottom); panel.Children.Add(footer);
        var body = new Grid { Margin = new(0,0,0,15) }; body.ColumnDefinitions.Add(new() { Width = new GridLength(1.2,GridUnitType.Star) }); body.ColumnDefinitions.Add(new() { Width = new GridLength(1,GridUnitType.Star) }); body.Children.Add(results);
        var infoPanel = new DockPanel { Margin = new(20,5,0,0) }; Grid.SetColumn(infoPanel,1); body.Children.Add(infoPanel);
        DockPanel.SetDock(source,Dock.Bottom); infoPanel.Children.Add(source);
        var info = new StackPanel(); info.Children.Add(infoTitle); info.Children.Add(infoMeta); info.Children.Add(infoNotes);
        infoPanel.Children.Add(new ScrollViewer { Content = info, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); panel.Children.Add(body);
        results.DisplayMemberPath = "Name";
        var template = new DataTemplate(typeof(CatalogEntry)); var stack = new FrameworkElementFactory(typeof(StackPanel));
        var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Name")); name.SetValue(TextBlock.FontSizeProperty,15d); stack.AppendChild(name);
        var detail = new FrameworkElementFactory(typeof(TextBlock)); detail.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Description")); detail.SetValue(TextBlock.FontSizeProperty,11d); detail.SetValue(TextBlock.OpacityProperty,.65); detail.SetValue(TextBlock.MarginProperty,new Thickness(0,5,0,0)); stack.AppendChild(detail); template.VisualTree=stack; results.DisplayMemberPath=""; results.ItemTemplate=template;
        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        void Filter() { var term = search.Text.Trim(); var matches = catalog.Entries.Where(e => term.Split(' ',StringSplitOptions.RemoveEmptyEntries).All(word => e.Name.Contains(word,StringComparison.OrdinalIgnoreCase))).ToList(); results.ItemsSource=matches.Take(200).ToList(); status.Text=F("{0:N0} Treffer · {1:N0} Modelle · maximal 200 angezeigt", matches.Count, catalog.Entries.Count); }
        search.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); }; debounce.Tick += (_, _) => { debounce.Stop(); Filter(); };
        var infoDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        async Task<CatalogInfo> GetInfo(CatalogEntry entry)
        { if(infoCache.TryGetValue(entry.Path,out var cached))return cached; var value=await catalog.GetInfoAsync(entry); infoCache[entry.Path]=value; return value; }
        results.SelectionChanged += (_, _) =>
        {
            infoRevision++; infoDebounce.Stop();
            if(results.SelectedItem is not CatalogEntry entry) { source.IsEnabled=false; infoTitle.Text=T("Ein Preset auswählen"); infoMeta.Text=""; infoNotes.Text=""; return; }
            source.IsEnabled=true; infoTitle.Text=entry.Name;
            infoMeta.Text=F("Messquelle: {0}\nKategorie / Messaufbau: {1}\nZielkurve: {2}", entry.Measurement, entry.Category, T("im Index nicht angegeben"));
            infoNotes.Text=T("Zusatzinfos werden geladen …"); infoDebounce.Start();
        };
        infoDebounce.Tick += async (_, _) =>
        {
            infoDebounce.Stop(); if(results.SelectedItem is not CatalogEntry entry)return; var revision=infoRevision;
            try { var value=await GetInfo(entry); if(closed || revision!=infoRevision)return; infoMeta.Text=F("Messquelle: {0}\nKategorie / Messaufbau: {1}\nZielkurve: {2}", value.Measurement, value.Category, value.TargetCurve ?? T("in der Quelle nicht angegeben")); infoNotes.Text=T("Originalhinweise aus AutoEQ\n\n")+value.Notes; }
            catch(Exception) { if(!closed && revision==infoRevision)infoNotes.Text=T("Zusatzinfos derzeit nicht verfügbar. Bereits geladene Presets bleiben offline nutzbar."); }
        };
        source.Click += (_, _) => { if(results.SelectedItem is CatalogEntry entry)try { Process.Start(new ProcessStartInfo(entry.SourceUrl) { UseShellExecute=true }); }catch(Exception ex){status.Text=ex.Message;} };
        refresh.Click += async (_, _) => { refresh.IsEnabled=false; try { status.Text=T("AutoEQ-Katalog wird aktualisiert …"); await catalog.RefreshAsync(); Filter(); } catch(Exception ex){ status.Text=T("Offline / Aktualisierung fehlgeschlagen: ")+ex.Message; } finally { refresh.IsEnabled=true; } };
        download.Click += async (_, _) =>
        {
            if(results.SelectedItem is not CatalogEntry entry) { status.Text=T("Bitte ein Kopfhörermodell auswählen."); return; }
            download.IsEnabled=false;
            try
            {
                status.Text=T("Preset wird geladen …"); var profile=await catalog.DownloadAsync(entry);
                try { var metadata=await GetInfo(entry); profile.TargetCurve=metadata.TargetCurve; profile.Notes=metadata.Notes; }
                catch(Exception) { profile.Notes=T("Zusätzliche Quellhinweise waren beim Import nicht verfügbar."); }
                if(closed)return;
                if(preview(profile)) { ProfileLoaded=true; Close(); } else status.Text=T("Vorschau geschlossen · gespeicherte Profile bleiben links erreichbar");
            }
            catch(Exception ex) { status.Text=T("Preset nicht geladen: ")+ex.Message; }
            finally { download.IsEnabled=true; }
        };
        Closed += (_, _) => { closed=true; debounce.Stop(); infoDebounce.Stop(); }; Loaded += (_, _) => search.Focus(); Filter();
    }
}

