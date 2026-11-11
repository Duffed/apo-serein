using static Serein.Core.UiLanguage;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Serein.Core;

public static class PeaceFormat
{
    // Peace IDs 0–7, 14–17; see docs/FORMAT.md. Slope/order variants are rejected.
    static readonly Dictionary<int, string> Map = new() { [0] = "PK", [1] = "LPQ", [2] = "HPQ", [3] = "BP", [4] = "LS", [5] = "HS", [6] = "NO", [7] = "AP", [14] = "LSC", [15] = "HSC", [16] = "LS", [17] = "HS" };
    public static Profile Import(string text, string name)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var section = ""; var issues = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('\uFEFF'); if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1]; if (!sections.TryAdd(section, new(StringComparer.OrdinalIgnoreCase))) issues.Add(T("Doppelter Abschnitt: ") + section); continue; }
            var pos = line.IndexOf('=');
            if (pos < 1 || !sections.ContainsKey(section)) { issues.Add(T("Ungültige Peace-Zeile: ") + line); continue; }
            if (!sections[section].TryAdd(line[..pos], line[(pos + 1)..])) issues.Add(T("Doppelter Schlüssel: ") + line[..pos]);
        }
        double Get(string s, string key, double fallback = 0)
        {
            if (!sections.TryGetValue(s, out var dict) || !dict.TryGetValue(key, out var val)) return fallback;
            if (!double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) { issues.Add(s + "/" + key + T(": ungültige Zahl")); return fallback; }
            return n;
        }
        foreach (var (s, entries) in sections)
        {
            if (s is "Speakers" or "Sliders") continue; // labels only
            if (Regex.IsMatch(s, @"^(Frequencies|Qualities)[1-8]$")) continue; // dormant per-channel frequency/Q defaults; gains checked below
            if (Regex.IsMatch(s, @"^Gains[1-8]$")) { if (entries.Any(x => !double.TryParse(x.Value, CultureInfo.InvariantCulture, out var n) || n != 0)) issues.Add(T("Individuelle Kanal-Gains: ") + s); continue; }
            if (s.Equals("General", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (key, val) in entries)
                    if (key is not "PreAmp" and not "Description" and not "GraphicEQ") issues.Add(T("Zusätzliche Peace-Einstellung: ") + key);
                continue;
            }
            if (s is "Frequencies" or "Gains" or "Qualities" or "Filters" or "Disabled")
            {
                var prefix = s switch { "Frequencies" => "Frequency", "Gains" => "Gain", "Qualities" => "Quality", "Filters" => "Filter", _ => "Disabled" };
                foreach (var key in entries.Keys) if (!Regex.IsMatch(key, "^" + prefix + @"\d+$")) issues.Add(T("Unbekannter Schlüssel: ") + s + "/" + key);
                continue;
            }
            if (entries.Count > 0) issues.Add(T("Nicht unterstützter Peace-Abschnitt: ") + s);
        }
        var p = new Profile { Name = name, Preamp = Get("General", "PreAmp") };
        if (sections.TryGetValue("General", out var general) && general.TryGetValue("Description", out var description)) p.Notes = description;
        bool graphic = Get("General", "GraphicEQ") != 0;
        if (!sections.TryGetValue("Frequencies", out var frequencies)) issues.Add(T("Frequenzen fehlen."));
        else foreach (var key in frequencies.Keys.OrderBy(k => int.TryParse(Regex.Match(k, @"\d+$").Value, out var i) ? i : int.MaxValue))
        {
            var id = Regex.Match(key, @"\d+$").Value;
            var f = Get("Frequencies", key); var gain = Get("Gains", "Gain" + id); var q = Get("Qualities", "Quality" + id, 1.41);
            var typeId = Get("Filters", "Filter" + id);
            if (typeId != Math.Truncate(typeId) || !Map.TryGetValue((int)typeId, out var type)) { issues.Add(F("Filter {0}: nicht unterstützter Typ {1}", id, typeId)); continue; }
            var enabled = Get("Disabled", "Disabled" + id) == 0;
            if (graphic)
            { if (typeId != 0 || !enabled) issues.Add(T("Grafischer Peace-EQ mit individuellen Filtertypen/deaktivierten Bändern.")); p.Graphic.Add(new() { Frequency = f, Gain = gain }); }
            else p.Filters.Add(new() { Frequency = f, Gain = gain, Q = q, Type = type, Enabled = enabled, UseQ = typeId is not 4 and not 5 });
        }
        if (frequencies != null)
            foreach (var s in new[] { "Gains", "Qualities", "Filters", "Disabled" })
                if (sections.TryGetValue(s, out var values)) foreach (var k in values.Keys)
                    if (!frequencies.ContainsKey("Frequency" + Regex.Match(k, @"\d+$").Value)) issues.Add(T("Einstellung ohne Frequenz: ") + s + "/" + k);
        try { ApoFormat.Validate(p); } catch (InvalidDataException e) { issues.Add(e.Message); }
        if (issues.Count > 0) throw new ImportException(issues);
        p.PresetOriginal = EqSettings.Capture(p); return p;
    }
}
