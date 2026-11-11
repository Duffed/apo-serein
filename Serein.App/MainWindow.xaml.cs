using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Serein.Core;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace Serein.App;
public partial class MainWindow : Window
{
    public static readonly DependencyProperty CompactProfileViewProperty = DependencyProperty.Register(nameof(CompactProfileView), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
    public bool CompactProfileView { get => (bool)GetValue(CompactProfileViewProperty); set => SetValue(CompactProfileViewProperty, value); }
    readonly bool smoke, interactiveTest;
    readonly string dataDirectory, statePath;
    readonly AppState state;
    readonly ObservableCollection<Profile> profiles;
    ApoBackend backend;
    Profile current = null!;
    Profile? watchedProfile;
    bool updating, closing, configuring;
    int selectionRevision;
    string? lastDefault;
    string selectedDevice = "*";
    readonly List<Observable> subscribed = [];
    readonly DispatcherTimer editTimer = new() { Interval = TimeSpan.FromMilliseconds(85) };
    readonly DispatcherTimer deviceTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly Forms.NotifyIcon tray = new();
    public MainWindow(bool smokeMode = false, bool interactiveTestMode = false)
    {
        smoke = smokeMode;
        interactiveTest = interactiveTestMode;
        dataDirectory = Environment.GetEnvironmentVariable("SEREIN_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Serein");
        if (smoke) dataDirectory = Path.Combine(AppContext.BaseDirectory, "smoke-data");
        statePath = Path.Combine(dataDirectory, "profiles.json");
        state = ProfileStore.Load(statePath);
        if (state.Profiles.Count == 0) state.Profiles.Add(new Profile { Name = "Neutral", Favorite = true });
        PeaceImportResult? migration = null;
        if (!smoke && state.PeaceMigrationVersion < 1 && Directory.Exists(state.ConfigDirectory))
        {
            migration = PeaceMigration.Import(state, state.ConfigDirectory);
            state.PeaceMigrationVersion = 1; state.PeaceImportIssues = migration.Issues.ToList();
        }
        profiles = new(state.Profiles);
        backend = new(state.ConfigDirectory);
        Appearance.Apply(state.Appearance);
        InitializeComponent();
        if (interactiveTest) Title = "Serein · Equalizer · Test";
        DarkChrome.Attach(this);
        if (migration != null)
        { MigrationNotice.Text = $"{migration.Added} Peace-Profile übernommen" + (migration.Issues.Count > 0 ? $" · {migration.Issues.Count} Hinweise (siehe Peace-Übernahme)" : ""); MigrationNotice.Visibility = Visibility.Visible; }
        TypeColumn.ItemsSource = FilterTypes.Choices;
        updating = true;
        ThemeBox.SelectedIndex = Appearance.IsLight ? 0 : 1;
        CompactProfileView = state.CompactProfiles; CompactProfilesBox.IsChecked = state.CompactProfiles;
        var sidebarWidth = double.IsFinite(state.SidebarWidth) ? Math.Clamp(state.SidebarWidth, 210, 480) : 245;
        SidebarColumn.Width = new GridLength(sidebarWidth); Width = Math.Max(1220, sidebarWidth + 800); MinWidth = Math.Max(1000, sidebarWidth + 800);
        UndoDeleteButton.Visibility = state.TrashedProfiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DeviceBox.ItemsSource = Devices.List(); lastDefault = Devices.DefaultId();
        var devices = (List<AudioDevice>)DeviceBox.ItemsSource;
        DeviceBox.SelectedItem = devices.FirstOrDefault(d => d.Id == lastDefault) ?? devices[0];
        selectedDevice = ((AudioDevice)DeviceBox.SelectedItem).Id;
        ProfileList.ItemsSource = profiles;
        var selected = state.Devices.TryGetValue(selectedDevice, out var mapped) ? mapped : state.Selected;
        updating = false;
        ProfileList.SelectedItem = profiles.FirstOrDefault(p => p.Id == selected) ?? profiles[0];
        editTimer.Tick += async (_, _) => { editTimer.Stop(); await CommitChanges(); };
        deviceTimer.Tick += async (_, _) => await PollDevices();
        if (!smoke) deviceTimer.Start();
        tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application; tray.Text = "Serein · Equalizer"; tray.Visible = !smoke;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        BuildTray();
        Closing += WindowClosing;
        ContentRendered += async (_, _) =>
        {
            ProfileList.ScrollIntoView(current);
            if (smoke) { if (interactiveTest) { Status.Text = "Testmodus · keine Audioänderung oder Speicherung von Benutzerprofilen"; return; } File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup-ms.txt"), (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture)); CaptureSmoke(); return; }
            Status.Text = backend.Check().Message;
            if (backend.Check().Connected) await ApplyCurrent();
        };
    }
    void RefreshProfiles(Guid? selected = null)
    {
        var text = ProfileSearch.Text?.Trim() ?? "";
        updating = true;
        ProfileList.ItemsSource = profiles.Where(p => p.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        ProfileList.SelectedItem = profiles.FirstOrDefault(p => p.Id == (selected ?? current?.Id));
        updating = false;
    }
    void SearchProfiles(object sender, TextChangedEventArgs e) { if (profiles != null && IsInitialized) RefreshProfiles(); }
    void SelectProfile(object sender, SelectionChangedEventArgs e)
    {
        if (updating || ProfileList.SelectedItem is not Profile p) return;
        if (current != null && profiles.Contains(current))
        {
            if (!FilterGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !FilterGrid.CommitEdit(DataGridEditingUnit.Row, true))
            { updating = true; ProfileList.SelectedItem = current; updating = false; Status.Text = "Bitte zuerst die ungültige Filtereingabe korrigieren."; return; }
            try { ApoFormat.Validate(current); }
            catch (Exception ex) { updating = true; ProfileList.SelectedItem = current; updating = false; Error(ex); return; }
        }
        Curve.CancelEdit(); if (current != null) HistoryFor(current).Record(); editTimer.Stop(); current = p; state.Selected = p.Id; state.Devices[selectedDevice] = p.Id;
        if (p.Adjustment.Count == 0) foreach (var hz in Response.BandFrequencies(10)) p.Adjustment.Add(new() { Frequency = hz });
        _ = HistoryFor(current);
        AttachProfile(); Populate(); SaveState(); BuildTray();
        _ = RememberPresetOriginal(p);
        if (IsLoaded) _ = ApplyCurrent();
    }
    void AttachProfile()
    {
        foreach (var obj in subscribed) obj.PropertyChanged -= Changed;
        subscribed.Clear();
        if (watchedProfile != null) { watchedProfile.Filters.CollectionChanged -= CollectionChanged; watchedProfile.Graphic.CollectionChanged -= CollectionChanged; watchedProfile.Adjustment.CollectionChanged -= CollectionChanged; }
        watchedProfile = current;
        current.Filters.CollectionChanged += CollectionChanged; current.Graphic.CollectionChanged += CollectionChanged; current.Adjustment.CollectionChanged += CollectionChanged;
        foreach (var obj in new Observable[] { current }.Concat(current.Filters).Concat(current.Graphic).Concat(current.Adjustment)) { obj.PropertyChanged += Changed; subscribed.Add(obj); }
    }
    void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { if (updating) return; AttachProfile(); UpdateCurveLayers(); ScheduleChange(); }
    void Changed(object? sender, PropertyChangedEventArgs e) { if (!updating) ScheduleChange(); }
    void ScheduleChange() { UpdateSaveButton(); editTimer.Stop(); editTimer.Start(); }
    void Populate()
    {
        updating = true;
        ProfileTitle.Text = current.Name; FavoriteBox.IsChecked = current.Favorite;
        PreampSlider.Minimum = Math.Min(-40, current.Preamp); PreampSlider.Maximum = Math.Max(20, current.Preamp);
        PreampSlider.Value = current.Preamp; PreampValue.Text = current.Preamp.ToString("0.###", CultureInfo.GetCultureInfo("de-DE"));
        AutoHeadroomBox.IsChecked = current.AutoHeadroom;
        SourceLabel.Text = current.SourceSummary + " · für Details klicken"; SourceLabel.ToolTip = current.Source;
        FilterGrid.ItemsSource = current.Filters; BandList.ItemsSource = current.Adjustment;
        Power.IsChecked = !state.Bypass;
        Power.Content = state.Bypass ? "EQ aus" : "EQ ein";
        Power.ToolTip = state.Bypass ? "EQ umgangen. Klicken zum Einschalten." : "EQ eingeschaltet. Der Status unten zeigt, ob die Konfiguration geschrieben wurde. Klicken zum Umgehen.";
        UpdateCurveLayers(); updating = false; RefreshCurve(); UpdateHistoryButtons(); UpdateSaveButton();
    }
    void RefreshCurve()
    {
        ApoFormat.Validate(current); Curve.Profile = current; Curve.Bypass = state.Bypass; Curve.InvalidateVisual();
        var peak = Response.Peak(current) + Response.EffectivePreamp(current);
        HeadroomLabel.Text = state.Bypass ? "EQ umgangen" : peak > .0001 ? $"Spitze +{peak:0.0} dB · Übersteuerung möglich" : $"Headroom ca. {Math.Max(0, -peak):0.0} dB";
    }
    async Task CommitChanges()
    {
        if (current == null) return;
        try
        {
            if (!FilterGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !FilterGrid.CommitEdit(DataGridEditingUnit.Row, true)) { Status.Text = "Ungültige Filtereingabe · Änderung nicht angewendet"; return; }
            ApoFormat.Validate(current); RefreshCurve(); HistoryFor(current).Record(); UpdateHistoryButtons(); if (SaveState()) await ApplyCurrent();
        }
        catch (Exception ex) { Status.Text = "Änderung nicht angewendet: " + ex.Message; }
    }
    bool SaveState()
    {
        if (smoke) { MarkProfilesSaved(); return true; }
        state.Profiles = profiles.ToList();
        try { foreach (var p in profiles) ApoFormat.Validate(p); ProfileStore.Save(statePath, state); MarkProfilesSaved(); return true; }
        catch (Exception ex) { Status.Text = "Speichern fehlgeschlagen: " + ex.Message; UpdateSaveButton(); return false; }
    }
    async Task ApplyCurrent()
    {
        if (smoke || current == null || configuring) return;
        var request = ++selectionRevision;
        try
        {
            ApoFormat.Validate(current);
            var check = backend.Check(); if (!check.Connected) { Status.Text = check.Message; return; }
            var target = selectedDevice;
            if (target != "*" && !Devices.List().Any(d => d.Id == target)) { Status.Text = "Ausgabegerät nicht verfügbar · Ausgabe angehalten"; return; }
            var result = await backend.ApplyAsync(current, target == "*" ? null : target, state.Bypass);
            if (request == selectionRevision && result.Written) Status.Text = result.Message;
        }
        catch (Exception ex) { if (request == selectionRevision) Status.Text = "Nicht angewendet: " + ex.Message; }
    }
    void PreampChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (updating || current == null) return; current.Preamp = e.NewValue; PreampValue.Text = e.NewValue.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));
    }
    void CommitPreamp(object sender, RoutedEventArgs e)
    {
        if (updating || current == null) return;
        if (!double.TryParse(PreampValue.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || Math.Abs(value) > 100)
        { Status.Text = "Preamp ungültig (−100 bis +100 dB)."; PreampValue.Text = current.Preamp.ToString(CultureInfo.GetCultureInfo("de-DE")); return; }
        updating = true; PreampSlider.Minimum = Math.Min(-40, value); PreampSlider.Maximum = Math.Max(20, value); PreampSlider.Value = value; updating = false; current.Preamp = value;
    }
    void PreampKey(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { CommitPreamp(sender, e); Keyboard.ClearFocus(); } }
    void AutoHeadroomChanged(object sender, RoutedEventArgs e) { if (updating || current == null) return; current.AutoHeadroom = AutoHeadroomBox.IsChecked == true; ScheduleChange(); }
    void FavoriteChanged(object sender, RoutedEventArgs e) { if (updating || current == null) return; current.Favorite = FavoriteBox.IsChecked == true; RefreshProfiles(); SaveState(); BuildTray(); }
    async void ToggleBypass(object sender, RoutedEventArgs e) { state.Bypass = !state.Bypass; Populate(); SaveState(); BuildTray(); await ApplyCurrent(); }
    async void SaveProfile(object sender, RoutedEventArgs e) { if (!SaveButton.IsEnabled) return; CommitPreamp(sender, e); editTimer.Stop(); await CommitChanges(); }
    void NewProfile(object sender, RoutedEventArgs e) { var name = Prompt("Neues Profil", "Profilname", "Neues Profil"); if (name != null) AddProfile(new() { Name = name }); }
    void CompactProfilesChanged(object sender, RoutedEventArgs e)
    {
        if (updating || current == null) return;
        CompactProfileView = CompactProfilesBox.IsChecked == true; state.CompactProfiles = CompactProfileView; SaveState();
    }
    void ChangeAppearance(object sender, SelectionChangedEventArgs e)
    {
        if (updating || current == null || ThemeBox.SelectedIndex < 0) return;
        state.Appearance = ThemeBox.SelectedIndex == 0 ? "Light" : "Dark";
        Appearance.Apply(state.Appearance); SaveState();
    }
    void SidebarResized(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    { state.SidebarWidth = SidebarColumn.ActualWidth; MinWidth = Math.Max(1000, state.SidebarWidth + 800); SaveState(); }
    void ResetSidebarWidth(object sender, MouseButtonEventArgs e)
    { SidebarColumn.Width = new GridLength(245); state.SidebarWidth = 245; MinWidth = 1045; SaveState(); }
    void DeleteProfileFromRow(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is System.Windows.Controls.Button { DataContext: Profile profile }) DeleteProfile(profile);
    }
    void DeleteProfile(Profile profile)
    {
        if (!profiles.Contains(profile)) return;
        bool active = ReferenceEquals(current, profile);
        var replacement = profiles.FirstOrDefault(p => p != profile && ProfileLibrary.IsNeutral(p)) ?? profiles.FirstOrDefault(p => p != profile);
        var message = $"„{profile.Name}“ aus der Bibliothek löschen?\n" + (active ? $"Danach wird „{replacement?.Name ?? "Neutral"}“ geladen.\n" : "") + "Die ursprüngliche Peace-Datei bleibt erhalten.";
        if (MessageBox.Show(this, message, "Profil löschen", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        if (active) { FilterGrid.CancelEdit(DataGridEditingUnit.Cell); FilterGrid.CancelEdit(DataGridEditingUnit.Row); editTimer.Stop(); }
        state.Profiles = profiles.ToList(); ProfileLibrary.Remove(state, profile.Id);
        updating = true; ProfileList.ItemsSource = null; profiles.Clear(); foreach (var p in state.Profiles) profiles.Add(p); updating = false;
        ProfileSearch.Text = ""; RefreshProfiles(); ProfileList.SelectedItem = profiles.First(p => p.Id == state.Selected);
        UndoDeleteButton.Visibility = Visibility.Visible; SaveState(); BuildTray();
    }
    void UndoDelete(object sender, RoutedEventArgs e)
    {
        state.Profiles = profiles.ToList(); var restored = ProfileLibrary.RestoreLast(state); if (restored == null) return;
        if (!profiles.Any(p => p.Id == restored.Id)) profiles.Add(restored);
        RefreshProfiles(); SaveState(); BuildTray(); UndoDeleteButton.Visibility = state.TrashedProfiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Status.Text = $"„{restored.Name}“ wieder in der Bibliothek";
    }
    void DuplicateProfile(object sender, RoutedEventArgs e) { var p = current.Clone(); p.Name += " · Kopie"; AddProfile(p); }
    void RenameProfile(object sender, RoutedEventArgs e) { var name = Prompt("Profil umbenennen", "Profilname", current.Name); if (name == null) return; current.Name = name; ProfileTitle.Text = name; RefreshProfiles(); SaveState(); BuildTray(); }
    bool AddProfile(Profile p, bool activate = true)
    {
        if (activate && current != null)
        {
            if (!FilterGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !FilterGrid.CommitEdit(DataGridEditingUnit.Row, true)) { Error(new InvalidDataException("Bitte zuerst die ungültige Filtereingabe korrigieren.")); return false; }
            try { ApoFormat.Validate(current); } catch (Exception ex) { Error(ex); return false; }
        }
        if (p.SourceFile != null) state.SuppressedPeaceFiles.RemoveAll(path => string.Equals(path, p.SourceFile, StringComparison.OrdinalIgnoreCase));
        profiles.Add(p); ProfileSearch.Text = ""; RefreshProfiles();
        if (activate) ProfileList.SelectedItem = p;
        SaveState(); BuildTray();
        return !activate || ReferenceEquals(current, p);
    }
    void SetBands(int count)
    {
        if (current.Adjustment.Any(p => p.Gain != 0) && MessageBox.Show("Die persönliche grafische Anpassung wird zurückgesetzt. Importierte Korrekturen bleiben erhalten.", "Bandanzahl ändern", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        updating = true; current.Adjustment.Clear(); foreach (var f in Response.BandFrequencies(count)) current.Adjustment.Add(new() { Frequency = f }); updating = false;
        AttachProfile(); BandList.ItemsSource = current.Adjustment; ScheduleChange();
    }
    void Bands10(object sender, RoutedEventArgs e) => SetBands(10);
    void Bands31(object sender, RoutedEventArgs e) => SetBands(31);
    void ResetGraphic(object sender, RoutedEventArgs e) { foreach (var p in current.Adjustment) p.Gain = 0; }
    void AddFilter(object sender, RoutedEventArgs e) => current.Filters.Add(new());
    void RemoveFilter(object sender, RoutedEventArgs e) { if (FilterGrid.SelectedItem is EqFilter f) current.Filters.Remove(f); }
    void FilterEdited(object? sender, DataGridCellEditEndingEventArgs e) => ScheduleChange();
    void SelectDevice(object sender, SelectionChangedEventArgs e)
    {
        if (updating || DeviceBox.SelectedItem is not AudioDevice d) return;
        try
        {
            if (!FilterGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !FilterGrid.CommitEdit(DataGridEditingUnit.Row, true)) throw new InvalidDataException("Ungültige Filtereingabe.");
            ApoFormat.Validate(current);
        }
        catch (Exception ex)
        { updating = true; DeviceBox.SelectedItem = ((IEnumerable<AudioDevice>)DeviceBox.ItemsSource).FirstOrDefault(item => item.Id == selectedDevice); updating = false; Status.Text = "Gerätewechsel angehalten: " + ex.Message; return; }
        selectedDevice = d.Id;
        ProfileSearch.Text = ""; RefreshProfiles();
        var mapped = DeviceProfiles.Resolve(state, profiles, d.Id, d.Name, current, false);
        if (ReferenceEquals(current, mapped)) { SaveState(); _ = ApplyCurrent(); } else ProfileList.SelectedItem = mapped;
    }
    bool polling;
    async Task PollDevices()
    {
        if (polling || configuring) return; polling = true;
        try
        {
            var snapshot = await Task.Run(() => (List: Devices.List(), Default: Devices.DefaultId()));
            var old = ((IEnumerable<AudioDevice>)DeviceBox.ItemsSource).ToList();
            if (!old.SequenceEqual(snapshot.List)) { updating = true; DeviceBox.ItemsSource = snapshot.List; DeviceBox.SelectedItem = snapshot.List.FirstOrDefault(d => d.Id == selectedDevice); updating = false; }
            if (snapshot.Default != lastDefault)
            {
                lastDefault = snapshot.Default;
                if (snapshot.Default != null && snapshot.List.FirstOrDefault(d => d.Id == snapshot.Default) is AudioDevice next)
                {
                    // Never attach the previous device's correction to a newly seen output automatically.
                    DeviceProfiles.Resolve(state, profiles, next.Id, next.Name, current, true); RefreshProfiles();
                    DeviceBox.SelectedItem = next;
                }
            }
            if (!backend.Check().Connected) Status.Text = backend.Check().Message;
            else if (selectedDevice != "*" && !snapshot.List.Any(d => d.Id == selectedDevice)) Status.Text = "Ausgabegerät getrennt · Ausgabe angehalten";
        }
        catch (Exception ex) { Status.Text = "Geräteprüfung: " + ex.Message; }
        finally { polling = false; }
    }
    void OpenImportMenu(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { ContextMenu: { } menu } button)
        { menu.PlacementTarget = button; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; menu.IsOpen = true; }
    }
    void ImportFile(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "EQ-Profile (*.peace;*.txt)|*.peace;*.txt|Alle Dateien|*.*", Multiselect = true, InitialDirectory = state.ConfigDirectory };
        if (dialog.ShowDialog(this) != true) return;
        foreach (var file in dialog.FileNames) try
        { if (new FileInfo(file).Length > 2_000_000) throw new InvalidDataException("Datei zu groß."); var p = ApoFormat.Import(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file)); p.Source = "Import: " + Path.GetFileName(file); p.SourceFile = Path.GetFullPath(file); Preview(p); }
        catch (Exception ex) { Error(new Exception(Path.GetFileName(file) + "\n" + ex.Message)); }
    }
    void ImportText(object sender, RoutedEventArgs e)
    {
        var box = new System.Windows.Controls.TextBox { AcceptsReturn = true, AcceptsTab = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas"), Text = "Preamp: 0 dB\nFilter 1: ON PK Fc 1000 Hz Gain 0 dB Q 1" };
        var window = Dialog("APO-Text importieren", 720, 500, out var panel);
        var button = new System.Windows.Controls.Button { Content = "Import prüfen", Margin = new(0,12,0,0) }; DockPanel.SetDock(button, Dock.Bottom); panel.Children.Add(button); panel.Children.Add(box);
        button.Click += (_, _) => { try { var p = ApoFormat.Import(box.Text, "Textimport"); p.Source = "Eingefügter APO-Text"; window.Close(); Preview(p); } catch (Exception ex) { Error(ex); } }; window.ShowDialog();
    }
    bool Preview(Profile p)
    {
        bool loaded = false;
        var window = Dialog("Preset-Vorschau · " + p.Name, 780, 690, out var panel);
        var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new(0,15,0,0) }; DockPanel.SetDock(buttons, Dock.Bottom);
        var save = new System.Windows.Controls.Button { Content = "In Bibliothek speichern" }; var load = new System.Windows.Controls.Button { Content = "Speichern und laden" }; var cancel = new System.Windows.Controls.Button { Content = "Abbrechen" };
        buttons.Children.Add(save); buttons.Children.Add(load); buttons.Children.Add(cancel); panel.Children.Add(buttons);
        var details = new StackPanel(); details.Children.Add(new TextBlock { Text = p.Name, FontSize = 24, Margin = new(0,0,0,10) });
        details.Children.Add(new TextBlock { Text = $"{p.Filters.Count} Filter · {p.Graphic.Count + p.Adjustment.Count} grafische Punkte · Preamp {p.Preamp:0.##} dB", Margin = new(0,0,0,10) });
        details.Children.Add(new TextBlock { Text = ProfileInfo(p), TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        AddSourceButton(details, p.Source);
        details.Children.Add(new CurveView { Profile = p, Height = 220, Margin = new(0,16,0,0) });
        details.Children.Add(new TextBlock { Text = "Nur speichern lässt den aktuellen EQ unverändert. Speichern und laden aktiviert dieses Profil und öffnet es in der Hauptansicht.", TextWrapping = TextWrapping.Wrap, FontSize = 12 }); panel.Children.Add(new ScrollViewer { Content = details, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        save.Click += (_, _) => { AddProfile(p, false); window.Close(); Status.Text = "Preset gespeichert · zum Aktivieren links auswählen"; };
        load.Click += (_, _) => { if (AddProfile(p, true)) { loaded = true; window.Close(); } }; cancel.Click += (_, _) => window.Close(); window.ShowDialog();
        return loaded;
    }
    void OpenCatalog(object sender, RoutedEventArgs e)
    { var window = new CatalogWindow(dataDirectory, Preview) { Owner = this }; window.ShowDialog(); if (window.ProfileLoaded) ShowFromTray(); }
    static string ProfileInfo(Profile p) => p.SourceSummary +
        (string.IsNullOrWhiteSpace(p.MeasurementSource) ? "" : "\nMessquelle: " + p.MeasurementSource) +
        (string.IsNullOrWhiteSpace(p.Category) ? "" : "\nKategorie / Messaufbau: " + p.Category) +
        "\nZielkurve: " + (string.IsNullOrWhiteSpace(p.TargetCurve) ? "In der Quelle nicht angegeben" : p.TargetCurve) +
        (string.IsNullOrWhiteSpace(p.SourceFile) ? "" : "\nOriginaldatei: " + p.SourceFile) +
        (string.IsNullOrWhiteSpace(p.Notes) ? "" : "\n\nHinweise der Quelle:\n" + p.Notes);
    static void AddSourceButton(StackPanel panel, string? source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != "https") return;
        var button = new System.Windows.Controls.Button { Content = "Originalquelle öffnen ↗", HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new(0,12,0,0) };
        button.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, "Quelle öffnen"); } }; panel.Children.Add(button);
    }
    void ShowProfileInfo(object sender, MouseButtonEventArgs e)
    {
        var profile = current;
        var window = Dialog("Profilinformationen · " + profile.Name, 700, 570, out var panel);
        var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = profile.Name, FontSize = 24, Margin = new(0,0,0,15) });
        var info = new TextBlock { Text = ProfileInfo(profile), TextWrapping = TextWrapping.Wrap }; stack.Children.Add(info); AddSourceButton(stack, profile.Source);
        const string prefix = "https://github.com/jaakkopasanen/AutoEq/tree/master/results/";
        if (string.IsNullOrWhiteSpace(profile.Notes) && profile.Source?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
        {
            window.Loaded += async (_, _) =>
            {
                info.Text += "\n\nZusatzinfos werden geladen …";
                try
                {
                    var path = profile.Source[prefix.Length..]; var entry = new CatalogEntry(profile.Name, path, Uri.UnescapeDataString(path.Split('/')[0]));
                    var value = await new AutoEqCatalog(Path.Combine(dataDirectory,"autoeq")).GetInfoAsync(entry);
                    profile.MeasurementSource = value.Measurement; profile.Category = value.Category; profile.TargetCurve = value.TargetCurve; profile.Notes = value.Notes;
                    info.Text = ProfileInfo(profile); SaveState();
                }
                catch (Exception) { info.Text = ProfileInfo(profile) + "\n\nZusatzinfos derzeit nicht verfügbar."; }
            };
        }
        panel.Children.Add(new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); window.ShowDialog();
    }
    void ImportPeaceProfiles(object sender, RoutedEventArgs e)
    {
        state.Profiles = profiles.ToList(); var result = PeaceMigration.Import(state, state.ConfigDirectory);
        foreach (var profile in state.Profiles.Where(p => !profiles.Contains(p))) profiles.Add(profile);
        state.PeaceMigrationVersion = 1; state.PeaceImportIssues = result.Issues.ToList(); RefreshProfiles(); SaveState(); BuildTray();
        MigrationNotice.Text = $"{result.Added} neue Peace-Profile · {result.Issues.Count} Hinweise"; MigrationNotice.Visibility = Visibility.Visible;
        var window = Dialog("Peace-Profile · Übernahmebericht", 780, 580, out var panel);
        panel.Children.Add(new System.Windows.Controls.TextBox { Text = $"{result.Added} neu übernommen, {result.Existing} bereits vorhanden oder bewusst entfernt, {result.Issues.Count} nicht unterstützt.\nDas aktive Profil bleibt ausgewählt. Originaldateien wurden nicht verändert.\n\n" + string.Join("\n\n", result.Issues), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); window.ShowDialog();
    }
    async void ManageApo(object sender, RoutedEventArgs e)
    {
        var window = Dialog("Equalizer APO · Einrichtung", 650, 480, out var panel);
        var buttons = new StackPanel { Margin = new(0,18,0,0) }; DockPanel.SetDock(buttons, Dock.Bottom);
        bool existingSetup = File.Exists(Path.Combine(state.ConfigDirectory,"Serein","setup.json"));
        var connect = new System.Windows.Controls.Button { Content = existingSetup ? "Neu verbinden und aktuelles Profil laden" : "Sichern und mit Serein verbinden", Margin = new(0,0,0,8) };
        var restore = new System.Windows.Controls.Button { Content = "Gesicherte Originalkonfiguration wiederherstellen" };
        var devices = new System.Windows.Controls.Button { Content = "APO-Geräte einrichten …", Margin = new(0,0,0,8) };
        buttons.Children.Add(devices); buttons.Children.Add(connect); buttons.Children.Add(restore); panel.Children.Add(buttons);
        var info = new StackPanel(); info.Children.Add(new TextBlock { Text = "Nach Peace wieder mit Serein verbinden.", FontSize = 20, Margin = new(0,0,0,14) });
        info.Children.Add(new TextBlock { Text = "Beende Peace und klicke auf Neu verbinden. Serein sichert die bisherige Konfiguration, übernimmt wieder die Steuerung und lädt das ausgewählte Profil. Frühere Sicherungen bleiben erhalten.", TextWrapping = TextWrapping.Wrap, Margin = new(0,0,0,14) });
        info.Children.Add(new TextBlock { Text = "Neues Ausgabegerät? Öffne die APO-Geräteeinrichtung, setze beim Gerät das Häkchen und bestätige die Auswahl.", TextWrapping = TextWrapping.Wrap, Margin = new(0,0,0,14) });
        var path = new System.Windows.Controls.TextBox { Text = state.ConfigDirectory }; info.Children.Add(path);
        info.Children.Add(new TextBlock { Text = "Konfigurationsordner · typischerweise C:\\Program Files\\EqualizerAPO\\config\nDie hörbare Funktion muss anschließend mit Wiedergabe geprüft werden.", TextWrapping = TextWrapping.Wrap, Margin = new(0,10,0,0), FontSize = 11 }); panel.Children.Add(info);
        async Task Run(bool restoring)
        {
            if (smoke) { Status.Text = "Einrichtung im Vorschau-Test deaktiviert"; return; }
            devices.IsEnabled = connect.IsEnabled = restore.IsEnabled = false;
            configuring = true; editTimer.Stop();
            try
            {
                await backend.WaitForIdleAsync();
                var candidate = new ApoBackend(path.Text.Trim());
                if (!File.Exists(Path.Combine(Path.GetDirectoryName(candidate.DirectoryPath)!, "EqualizerAPO.dll"))) throw new IOException("Kein Equalizer-APO-Konfigurationsordner.");
                var hash = ApoBackend.FileHash(candidate.RootPath);
                // An elevated, short-lived helper does setup only; the UI stays unprivileged.
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
                bool reconnect = !restoring && File.Exists(Path.Combine(candidate.OwnDirectory,"setup.json"));
                start.ArgumentList.Add(restoring ? "--restore" : reconnect ? "--reconnect" : "--setup"); start.ArgumentList.Add(candidate.DirectoryPath); start.ArgumentList.Add(hash); start.ArgumentList.Add(WindowsIdentity.GetCurrent().User!.Value);
                using var process = Process.Start(start)!; await process.WaitForExitAsync(); if (process.ExitCode != 0) throw new IOException("Einrichtung wurde abgebrochen oder ist fehlgeschlagen.");
                state.ConfigDirectory = candidate.DirectoryPath; backend = candidate; SaveState(); window.Close();
                Status.Text = candidate.Check().Message; configuring = false; if (!restoring) await ApplyCurrent();
            }
            catch (Exception ex) { Error(ex); }
            finally { configuring = false; devices.IsEnabled = connect.IsEnabled = restore.IsEnabled = true; }
        }
        devices.Click += async (_, _) =>
        {
            if (smoke) return;
            devices.IsEnabled = connect.IsEnabled = restore.IsEnabled = false;
            try
            {
                var installation = Directory.GetParent(Path.GetFullPath(path.Text.Trim()))?.FullName;
                var selector = installation is null ? null : new[] { "DeviceSelector.exe", "Configurator.exe" }
                    .Select(name => Path.Combine(installation, name)).FirstOrDefault(File.Exists);
                if (selector is null)
                    throw new FileNotFoundException("Die APO-Geräteeinrichtung wurde nicht gefunden. Prüfe den Konfigurationsordner von Equalizer APO.");

                using var process = Process.Start(new ProcessStartInfo(selector) { UseShellExecute = true, WorkingDirectory = installation! })
                    ?? throw new IOException("Die APO-Geräteeinrichtung konnte nicht geöffnet werden.");
                await process.WaitForExitAsync();
                await PollDevices();
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { } // UAC abgebrochen
            catch (Exception ex) { MessageBox.Show(window.IsVisible ? window : this, ex.Message, "APO-Geräteeinrichtung", MessageBoxButton.OK, MessageBoxImage.Information); }
            finally { devices.IsEnabled = connect.IsEnabled = restore.IsEnabled = true; }
        };
        connect.Click += async (_, _) => await Run(false); restore.Click += async (_, _) => await Run(true);
        window.ShowDialog(); await Task.CompletedTask;
    }
    void BuildTray()
    {
        var menu = new Forms.ContextMenuStrip(); menu.Items.Add("Serein öffnen", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add(state.Bypass ? "EQ einschalten" : "Eigenen EQ umgehen", null, (_, _) => Dispatcher.Invoke(() => ToggleBypass(this, new())));
        menu.Items.Add(new Forms.ToolStripSeparator());
        foreach (var p in profiles.Where(p => p.Favorite)) { var item = new Forms.ToolStripMenuItem(p.Name) { Checked = p.Id == current?.Id }; item.Click += (_, _) => Dispatcher.Invoke(() => { ProfileSearch.Text = ""; RefreshProfiles(); ProfileList.SelectedItem = p; }); menu.Items.Add(item); }
        menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Beenden", null, (_, _) => Dispatcher.Invoke(() => { closing = true; Close(); }));
        var old = tray.ContextMenuStrip; tray.ContextMenuStrip = menu; old?.Dispose();
    }
    void ShowFromTray() { Show(); WindowState = WindowState.Normal; Activate(); }
    void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (!closing && !smoke) { e.Cancel = true; Hide(); return; }
        editTimer.Stop(); deviceTimer.Stop(); SaveState(); tray.Visible = false; tray.Dispose();
    }
    Window Dialog(string title, double width, double height, out DockPanel panel)
    { panel = new DockPanel { Margin = new(24) }; panel.SetResourceReference(Panel.BackgroundProperty, "Bg"); var window = new Window { Title = title, Width = width, Height = height, MinWidth = 450, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel }; window.SetResourceReference(StyleProperty, typeof(Window)); DarkChrome.Attach(window); return window; }
    string? Prompt(string title, string label, string initial)
    {
        var window = Dialog(title, 440, 210, out var panel); var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = label, Margin = new(0,0,0,10) }); var box = new System.Windows.Controls.TextBox { Text = initial }; stack.Children.Add(box);
        var save = new System.Windows.Controls.Button { Content = "Übernehmen", Margin = new(0,16,0,0), IsDefault = true }; stack.Children.Add(save); panel.Children.Add(stack);
        string? result = null; save.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(box.Text) && box.Text.Trim().Length <= 100) { result = box.Text.Trim(); window.Close(); } }; window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); }; window.ShowDialog(); return result;
    }
    void Error(Exception ex) => MessageBox.Show(this, ex.Message, "Serein", MessageBoxButton.OK, MessageBoxImage.Information);
    void CaptureSmoke()
    {
        // In-process rendering check; never connects to the installed APO.
        try
        {
            var reconnectDirectory = Path.Combine(AppContext.BaseDirectory, "smoke-data", "reconnect-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(reconnectDirectory); var rootFile = Path.Combine(reconnectDirectory,"config.txt"); File.WriteAllText(rootFile,"Include: peace.txt\n");
            var reconnectBackend = new ApoBackend(reconnectDirectory); reconnectBackend.Setup(ApoBackend.FileHash(rootFile));
            const string peaceConfig = "# Peace erneut verwendet\nInclude: peace.txt\n"; File.WriteAllText(rootFile, peaceConfig);
            if (reconnectBackend.Check().Connected) throw new InvalidDataException("Peace-Wechsel wurde nicht erkannt.");
            reconnectBackend.Setup(ApoBackend.FileHash(rootFile), true);
            if (!reconnectBackend.Check().Connected) throw new InvalidDataException("Neuverbinden nach Peace fehlgeschlagen.");
            File.AppendAllText(reconnectBackend.ActivePath,"# externe Änderung\n"); reconnectBackend.Setup(ApoBackend.FileHash(rootFile), true);
            reconnectBackend.Restore(ApoBackend.FileHash(rootFile));
            if (File.ReadAllText(rootFile) != peaceConfig) throw new InvalidDataException("Peace-Sicherung beim Wiederverbinden verloren.");
            var disposableProfile = new Profile { Name = "Löschprobe", SourceFile = "local-test.peace" };
            var deletionState = new AppState { Profiles = [disposableProfile], Selected = disposableProfile.Id, Devices = new() { ["test"] = disposableProfile.Id } };
            ProfileLibrary.Remove(deletionState, disposableProfile.Id);
            if (deletionState.Profiles.Count != 1 || deletionState.Selected == disposableProfile.Id || deletionState.Devices["test"] == disposableProfile.Id) throw new InvalidDataException("Löschen des letzten Profils fehlgeschlagen.");
            if (ProfileLibrary.RestoreLast(deletionState)?.Id != disposableProfile.Id || deletionState.SuppressedPeaceFiles.Count != 0) throw new InvalidDataException("Wiederherstellung fehlgeschlagen.");
            var listStyle = (Style)FindResource(typeof(ListBoxItem));
            var template = (ControlTemplate)listStyle.Setters.OfType<Setter>().Single(s => s.Property == System.Windows.Controls.Control.TemplateProperty).Value;
            foreach (var trigger in template.Triggers.OfType<Trigger>().Where(t => t.Property == IsMouseOverProperty))
                if (trigger.Setters.OfType<Setter>().Any(s => s.Property == System.Windows.Controls.Border.BorderThicknessProperty || s.Property == MarginProperty || s.Property == System.Windows.Controls.Control.PaddingProperty)) throw new InvalidDataException("Hover verändert das Listenlayout.");
            var converter = new FlexibleDoubleConverter();
            foreach (var text in new[] { "0,7", "0.7" })
                if ((double)converter.ConvertBack(text, typeof(double), null!, CultureInfo.GetCultureInfo("de-DE")) != .7) throw new InvalidDataException("Dezimaleingabe-Test fehlgeschlagen.");
            var presetProbe = ApoFormat.Import("Preamp: -4 dB\nFilter 1: ON PK Fc 1000 Hz Gain -2 dB Q 1", "Originalprobe");
            presetProbe.Source = "Testquelle"; presetProbe.Favorite = true; var presetId = presetProbe.Id;
            presetProbe.Preamp = -9; presetProbe.Filters[0].Gain = 3; presetProbe.Adjustment.Add(new() { Frequency = 1000, Gain = 2 });
            var modifiedPreset = EqSettings.Capture(presetProbe); var resetHistory = new EqEditHistory(presetProbe); resetHistory.Begin(); presetProbe.PresetOriginal!.Restore(presetProbe); resetHistory.End();
            if (!presetProbe.PresetOriginal.Same(EqSettings.Capture(presetProbe), true) || presetProbe.Id != presetId || !presetProbe.Favorite || presetProbe.Source != "Testquelle" || !resetHistory.Undo() || !modifiedPreset.Same(EqSettings.Capture(presetProbe)) || !resetHistory.Redo()) throw new InvalidDataException("Preset-Wiederherstellung oder Rückgängig fehlerhaft.");
            var presetPath = Path.Combine(AppContext.BaseDirectory, "smoke-data", "preset-original.json"); ProfileStore.Save(presetPath, new AppState { Profiles = [presetProbe] });
            if (ProfileStore.Load(presetPath).Profiles[0].PresetOriginal?.Same(presetProbe.PresetOriginal) != true) throw new InvalidDataException("Original-Preset wurde nicht gespeichert.");
            var legacyPath = Path.Combine(AppContext.BaseDirectory, "smoke-data", "original.txt"); File.WriteAllText(legacyPath, "Preamp: -7 dB");
            var recovered = new PresetSources(Path.GetDirectoryName(legacyPath)!, "", Path.GetDirectoryName(legacyPath)!).ReadLocal(new Profile { SourceFile = legacyPath, Preamp = 3 });
            if (recovered?.Preamp != -7) throw new InvalidDataException("Originaldatei wurde nicht als Preset-Ausgangswert gelesen.");
            var historyProfile = new Profile { Preamp = -6, Filters = [new() { Type = "PK", Frequency = 1000, Gain = -2, Q = 1 }] };
            var historyProbe = new EqEditHistory(historyProfile); historyProbe.Begin();
            CurveEditing.ChangeQ(historyProfile.Filters[0], 120); historyProbe.Record(); CurveEditing.ChangeQ(historyProfile.Filters[0], 120);
            CurveEditing.MoveFilter(historyProfile, historyProfile.Filters[0], 5000, -3, false); historyProbe.End();
            double changedQ = historyProfile.Filters[0].Q, changedGain = historyProfile.Filters[0].Gain;
            if (historyProfile.Filters[0].Frequency != 1000 || changedQ <= 1 || !historyProbe.Undo() || historyProbe.CanUndo || historyProfile.Filters[0].Q != 1 || historyProfile.Filters[0].Gain != -2) throw new InvalidDataException("Shift/Q oder gruppiertes Rückgängig fehlgeschlagen.");
            if (!historyProbe.Redo() || historyProfile.Filters[0].Q != changedQ || historyProfile.Filters[0].Gain != changedGain) throw new InvalidDataException("Wiederholen der EQ-Änderung fehlgeschlagen.");
            historyProbe.Undo(); historyProfile.Preamp = -8; historyProbe.Record();
            if (historyProbe.CanRedo) throw new InvalidDataException("Neue Änderung hat Wiederholen nicht verworfen.");
            var qVariant = new Profile { Filters = [new() { Type = "LP", UseQ = false, Q = 100 }] }; var variantHistory = new EqEditHistory(qVariant); variantHistory.Begin();
            CurveEditing.ChangeQ(qVariant.Filters[0], -120); variantHistory.End();
            if (qVariant.Filters[0].Type != "LPQ" || !qVariant.Filters[0].UseQ || !variantHistory.Undo() || qVariant.Filters[0].Type != "LP" || qVariant.Filters[0].UseQ) throw new InvalidDataException("Q-Variante wird nicht wiederhergestellt.");
            foreach (var type in new[] { "PK", "LS", "HS", "LSC", "HSC" })
            {
                var dragProfile = new Profile { Preamp = -6 }; var dragFilter = new EqFilter { Type = type, Frequency = 1000, Gain = -3, Q = .71 };
                dragProfile.Filters.Add(dragFilter); dragProfile.Adjustment.Add(new EqPoint { Frequency = 1000, Gain = 2 });
                CurveEditing.MoveFilter(dragProfile, dragFilter, 1500, -4);
                if (Math.Abs(Response.RawAt(dragProfile, 1500) + dragProfile.Preamp + 4) > .05 || dragFilter.Q != .71 || dragFilter.Frequency != 1500) throw new InvalidDataException("Kurvenpunkt-Gain stimmt nicht: " + type);
            }
            var graphicDrag = new Profile { Preamp = -3, Graphic = [new() { Frequency = 100 }, new() { Frequency = 1000 }, new() { Frequency = 2000 }] };
            CurveEditing.MoveGraphic(graphicDrag, graphicDrag.Graphic[1], true, 20000, -8); ApoFormat.Validate(graphicDrag);
            if (graphicDrag.Graphic[1].Frequency >= 2000 || Math.Abs(Response.RawAt(graphicDrag, graphicDrag.Graphic[1].Frequency) - 3 + 8) > .01) throw new InvalidDataException("Grafischer Kurvenpunkt verletzt Reihenfolge oder Gain.");
            var test = ApoFormat.Import("Preamp: -6 dB\nFilter 1: ON LSC Fc 105 Hz Gain 5.5 dB Q 0.71\nFilter 2: ON PK Fc 2800 Hz Gain -3 dB Q 2", "Kopfhörer · Vorschau");
            test.Favorite = true; foreach (var hz in Response.BandFrequencies(10)) test.Adjustment.Add(new() { Frequency = hz }); AddProfile(test); Populate(); UpdateLayout();
            if (Directory.Exists(state.ConfigDirectory)) foreach (var fixture in Directory.GetFiles(state.ConfigDirectory, "#*.peace"))
            { try { AddProfile(ApoFormat.Import(File.ReadAllText(fixture), Path.GetFileNameWithoutExtension(fixture)), false); } catch (ImportException) { } }
            var originalOrder = profiles.ToArray(); var selectedBeforeOrder = current.Id;
            var assignmentsBeforeOrder = new Dictionary<string, Guid>(state.Devices);
            if (!MoveProfile(originalOrder[^1], originalOrder[0], false) || profiles[0] != originalOrder[^1] || current.Id != selectedBeforeOrder || ProfileList.SelectedItem != current || !state.Devices.OrderBy(p => p.Key).SequenceEqual(assignmentsBeforeOrder.OrderBy(p => p.Key))) throw new InvalidDataException("Umsortieren verändert die aktive Auswahl.");
            var orderPath = Path.Combine(AppContext.BaseDirectory, "smoke-data", "profile-order.json");
            ProfileStore.Save(orderPath, new AppState { Profiles = profiles.ToList(), Selected = current.Id });
            if (!ProfileStore.Load(orderPath).Profiles.Select(p => p.Id).SequenceEqual(profiles.Select(p => p.Id))) throw new InvalidDataException("Profilreihenfolge wurde nicht erhalten.");
            MoveProfile(originalOrder[^1], originalOrder[^2], true);
            var latencies = new List<double>();
            for (int i = 0; i < 40; i++) { var watch = Stopwatch.StartNew(); ProfileList.SelectedItem = profiles[i % profiles.Count]; UpdateLayout(); latencies.Add(watch.Elapsed.TotalMilliseconds); }
            latencies.Sort(); File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ui-switch-ms.txt"), $"p50={latencies[19]:F2}; p95={latencies[37]:F2}; max={latencies[^1]:F2}");
            ProfileList.SelectedItem = test; Populate(); UpdateLayout(); Curve.SelectFilter(test.Filters[^1]);
            double beforePresetButton = Curve.TranslatePoint(new Point(), this).Y;
            test.Filters[^1].Gain -= 1; UpdateHistoryButtons(); UpdateLayout();
            if (!SaveButton.IsEnabled) throw new InvalidDataException("Speichern ist trotz Änderung deaktiviert.");
            if (Math.Abs(Curve.TranslatePoint(new Point(), this).Y - beforePresetButton) > .01) throw new InvalidDataException("Preset-Button verschiebt die Kurve.");
            MarkProfilesSaved(); if (SaveButton.IsEnabled) throw new InvalidDataException("Speichern bleibt ohne offene Änderungen aktiv.");
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            int phase = 0; Window? catalogPreview = null; ComboBox? typePreview = null;
            void Capture(FrameworkElement visual, string name)
            {
                visual.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(visual); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name + ".png")); encoder.Save(file);
            }
            ThemeBox.SelectedIndex = 1;
            timer.Tick += (_, _) =>
            {
                try
                {
                    switch (phase++)
                    {
                        case 0:
                            Capture((FrameworkElement)Content, "Serein-Dunkel"); EqTabs.SelectedIndex = 1; UpdateLayout();
                            if (Curve.Layer != CurveLayer.Filters || (CurveLayerTabs.SelectedItem as CurveLayerChoice)?.Layer != CurveLayer.Filters) throw new InvalidDataException("Filteransicht und Kurven-Tabs sind nicht synchron.");
                            FilterGrid.CurrentCell = new DataGridCellInfo(current.Filters[0], TypeColumn); FilterGrid.BeginEdit(); FilterGrid.UpdateLayout();
                            typePreview = TypeColumn.GetCellContent(current.Filters[0]) as ComboBox ?? throw new InvalidDataException("Filtertyp-Editor fehlt.");
                            if (typePreview.SelectedValue as string != current.Filters[0].Type) throw new InvalidDataException("Filtertyp-Auswahl passt nicht zum gespeicherten Code.");
                            typePreview.IsDropDownOpen = true; break;
                        case 1:
                            Capture((FrameworkElement)Content, "Serein-Filter-Dunkel");
                            if (typePreview!.Template.FindName("PART_Popup", typePreview) is not System.Windows.Controls.Primitives.Popup { Child: FrameworkElement popup }) throw new InvalidDataException("Filtertyp-Auswahlliste fehlt.");
                            Capture(popup, "Serein-Filtertypen-Dunkel"); typePreview.IsDropDownOpen = false;
                            var originalType = current.Filters[0].Type; typePreview.SelectedValue = "PK";
                            if (current.Filters[0].Type != "PK") throw new InvalidDataException("Filtertyp wird nicht als APO-Code übernommen.");
                            typePreview.SelectedValue = originalType;
                            FilterGrid.CommitEdit(DataGridEditingUnit.Cell, true); FilterGrid.CommitEdit(DataGridEditingUnit.Row, true);
                            EqTabs.SelectedIndex = 0;
                            if (Curve.Layer != CurveLayer.Adjustment || !ReferenceEquals(BandList.ItemsSource, current.Adjustment)) throw new InvalidDataException("Grafische Ansicht und Kurven-Tabs sind nicht synchron.");
                            ThemeBox.SelectedIndex = 0; break;
                        case 2:
                            if (!Appearance.IsLight || state.Appearance != "Light") throw new InvalidDataException("Darstellungswechsel fehlgeschlagen.");
                            Capture((FrameworkElement)Content, "Serein-Hell"); EqTabs.SelectedIndex = 1; break;
                        case 3:
                            Capture((FrameworkElement)Content, "Serein-Filter-Hell");
                            catalogPreview = new CatalogWindow(dataDirectory, _ => false) { Owner = this }; catalogPreview.Show(); break;
                        case 4:
                            Capture((FrameworkElement)catalogPreview!.Content, "Serein-Katalog-Hell"); catalogPreview.Close();
                            ThemeBox.SelectedIndex = 1; EqTabs.SelectedIndex = 0; break;
                        case 5:
                            if (Appearance.IsLight) throw new InvalidDataException("Rückwechsel zu Dunkel fehlgeschlagen.");
                            Capture((FrameworkElement)Content, "Serein-Vorschau"); Status.Text = "Konfiguration angewendet"; OpenImportMenu(ImportButton, new()); break;
                        case 6:
                            Capture(ImportButton.ContextMenu, "Serein-Import-Menue"); ImportButton.ContextMenu.IsOpen = false;
                            if (Status.Parent is not Border { Visibility: Visibility.Collapsed }) throw new InvalidDataException("Erfolgsstatus wird noch als Text angezeigt.");
                            Capture((FrameworkElement)Content, "Serein-Status-Symbol"); Status.Text = "Nicht angewendet: Testmeldung"; break;
                        default:
                            if (Status.Parent is not Border { Visibility: Visibility.Visible }) throw new InvalidDataException("Fehlerstatus wird ausgeblendet.");
                            timer.Stop(); closing = true; Close(); break;
                    }
                }
                catch (Exception ex) { timer.Stop(); catalogPreview?.Close(); File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-error.txt"), ex.ToString()); closing = true; Close(); }
            };
            timer.Start();
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-error.txt"), ex.ToString()); closing = true; Close(); }
    }
}



