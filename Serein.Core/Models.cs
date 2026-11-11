using static Serein.Core.UiLanguage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Serein.Core;

public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void RaisePropertyChanged(string property) => PropertyChanged?.Invoke(this, new(property));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; PropertyChanged?.Invoke(this, new(name)); return true; }
}
public class EqFilter : Observable
{
    string type = "PK"; double frequency = 1000, gain, q = 1; bool enabled = true, useQ = true;
    public string Type { get => type; set => Set(ref type, value); }
    public double Frequency { get => frequency; set => Set(ref frequency, value); }
    public double Gain { get => gain; set => Set(ref gain, value); }
    public double Q { get => q; set => Set(ref q, value); }
    public bool Enabled { get => enabled; set => Set(ref enabled, value); }
    public bool UseQ { get => useQ; set => Set(ref useQ, value); }
}
public class EqPoint : Observable
{
    double frequency, gain;
    public double Frequency { get => frequency; set { if (Set(ref frequency, value)) RaisePropertyChanged(nameof(Label)); } }
    public double Gain { get => gain; set => Set(ref gain, value); }
    public string Label => Frequency >= 1000 ? (Frequency / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "k" : Frequency.ToString("0", CultureInfo.InvariantCulture);
    public double SliderMinimum => Math.Min(-24, Gain);
    public double SliderMaximum => Math.Max(24, Gain);
}
public class Profile : Observable
{
    string name = T("Neues Profil"); double preamp; bool favorite;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get => name; set => Set(ref name, value); }
    public double Preamp { get => preamp; set => Set(ref preamp, value); }
    public bool Favorite { get => favorite; set => Set(ref favorite, value); }
    public string? Source { get; set; }
    public string? SourceFile { get; set; }
    public string? MeasurementSource { get; set; }
    public string? TargetCurve { get; set; }
    public string? Category { get; set; }
    public string? Notes { get; set; }
    public EqSettings? PresetOriginal { get; set; }
    [JsonIgnore] public string SourceSummary
    {
        get
        {
            if (Source?.StartsWith("https://github.com/jaakkopasanen/AutoEq/", StringComparison.OrdinalIgnoreCase) == true)
            {
                var marker = Source.IndexOf("/results/", StringComparison.Ordinal);
                var measurement = MeasurementSource ?? (marker < 0 ? "" : Uri.UnescapeDataString(Source[(marker + 9)..].Split('/')[0]));
                return "AutoEQ" + (string.IsNullOrWhiteSpace(measurement) ? "" : " · " + measurement);
            }
            if (SourceFile?.EndsWith(".peace", StringComparison.OrdinalIgnoreCase) == true || Source?.EndsWith(".peace", StringComparison.OrdinalIgnoreCase) == true) return T("Peace · lokal");
            if (SourceFile != null || Source?.StartsWith("Import:") == true) return T("APO-Datei · lokal");
            return Source == "Eingefügter APO-Text" ? T("Eingefügter APO-Text") : Source ?? T("Eigenes Profil");
        }
    }
    public ObservableCollection<EqFilter> Filters { get; set; } = [];
    public ObservableCollection<EqPoint> Graphic { get; set; } = [];
    public ObservableCollection<EqPoint> Adjustment { get; set; } = [];
    public bool AutoHeadroom { get; set; }
    public Profile Clone() { var p = JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(this))!; p.Id = Guid.NewGuid(); return p; }
}
public class AppState
{
    public int Version { get; set; } = 1;
    public List<Profile> Profiles { get; set; } = [];
    public Guid? Selected { get; set; }
    public Dictionary<string, Guid> Devices { get; set; } = [];
    public string ConfigDirectory { get; set; } = @"C:\Program Files\EqualizerAPO\config";
    public bool Bypass { get; set; }
    public int PeaceMigrationVersion { get; set; }
    public List<string> PeaceImportIssues { get; set; } = [];
    public bool CompactProfiles { get; set; }
    public string Appearance { get; set; } = "Dark";
    public string Language { get; set; } = "System";
    public string AccentColor { get; set; } = "System";
    public double SidebarWidth { get; set; } = 245;
    public List<Profile> TrashedProfiles { get; set; } = [];
    public List<string> SuppressedPeaceFiles { get; set; } = [];
}
public static class ProfileStore
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static AppState Load(string path)
    {
        if (!File.Exists(path)) return new();
        var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(path), Json) ?? throw new InvalidDataException(T("Leere Profildatei."));
        if (state.Version != 1) throw new InvalidDataException(T("Unbekannte Profilversion; Daten wurden nicht verändert."));
        foreach (var p in state.Profiles) { ApoFormat.Validate(p); p.PresetOriginal?.Validate(); }
        return state;
    }
    public static void Save(string path, AppState state) => AtomicFile.Write(path, JsonSerializer.Serialize(state, Json));
}
public static class AtomicFile
{
    public static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, content, new System.Text.UTF8Encoding(false));
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(path)) File.Replace(temp, path, null);
                    else File.Move(temp, path);
                    break;
                }
                catch (Exception ex) when (attempt < 5 && (ex is IOException or UnauthorizedAccessException) && (ex.HResult & 0xFFFF) is 5 or 32 or 33)
                { Thread.Sleep(2 << attempt); }
            }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
