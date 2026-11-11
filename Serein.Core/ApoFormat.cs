using static Serein.Core.UiLanguage;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Serein.Core;

public sealed class ImportException(IEnumerable<string> issues) : Exception(T("Import nicht übernommen:\n") + string.Join("\n", issues.Distinct())) { }
public static partial class ApoFormat
{
    public static readonly string[] Types = ["PK", "LS", "HS", "LSC", "HSC", "LP", "HP", "LPQ", "HPQ", "NO", "AP", "BP"];
    static double Number(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    public static string N(double n) => n.ToString("0.########", CultureInfo.InvariantCulture);
    public static void Validate(Profile p)
    {
        if (!double.IsFinite(p.Preamp) || Math.Abs(p.Preamp) > 100) throw new InvalidDataException(T("Preamp muss zwischen −100 und +100 dB liegen."));
        if (p.Filters.Count > 512 || p.Graphic.Count > 2048 || p.Adjustment.Count > 2048) throw new InvalidDataException(T("Zu viele Filterpunkte."));
        foreach (var f in p.Filters)
            if (!Types.Contains(f.Type) || (f.Type is "PK" or "AP" && !f.UseQ) || !double.IsFinite(f.Frequency) || f.Frequency < 1 || f.Frequency > 24000 || !double.IsFinite(f.Gain) || Math.Abs(f.Gain) > 100 || !double.IsFinite(f.Q) || f.Q < .01 || f.Q > 100)
                throw new InvalidDataException(T("Filter ungültig: Frequenz 1–24000 Hz, Gain ±100 dB, Q 0,01–100."));
        foreach (var stage in new[] { p.Graphic, p.Adjustment })
        {
            double last = 0;
            foreach (var point in stage)
            { if (!double.IsFinite(point.Frequency) || point.Frequency <= last || point.Frequency > 24000 || !double.IsFinite(point.Gain) || Math.Abs(point.Gain) > 100) throw new InvalidDataException(T("Grafische EQ-Punkte müssen gültig und nach Frequenz sortiert sein.")); last = point.Frequency; }
        }
    }
    public static string Export(Profile p, bool bypass = false)
    {
        Validate(p);
        if (bypass) return "# Serein: eigener EQ umgangen\n";
        var b = new StringBuilder("# Serein – automatisch erzeugt\n");
        b.AppendLine("Preamp: " + N(Response.EffectivePreamp(p)) + " dB");
        var index = 1;
        foreach (var f in p.Filters)
            b.AppendLine($"Filter {index++}: {(f.Enabled ? "ON" : "OFF")} {f.Type} Fc {N(f.Frequency)} Hz" +
                (f.Type is "PK" or "LS" or "HS" or "LSC" or "HSC" ? $" Gain {N(f.Gain)} dB" : "") +
                (f.UseQ ? $" Q {N(f.Q)}" : ""));
        foreach (var stage in new[] { p.Graphic, p.Adjustment })
            if (stage.Count > 0) b.AppendLine("GraphicEQ: " + string.Join("; ", stage.Select(x => N(x.Frequency) + " " + N(x.Gain))));
        return b.ToString();
    }
    public static Profile Import(string text, string name)
    {
        if (text.Length > 2_000_000) throw new ImportException([T("Datei zu groß.")]);
        if (text.TrimStart('\uFEFF', ' ', '\r', '\n').StartsWith('[')) return PeaceFormat.Import(text, name);
        var p = new Profile { Name = name }; var issues = new List<string>(); int lineNo = 0, graphicCount = 0;
        foreach (var raw in text.Split('\n'))
        {
            lineNo++; var line = raw.Split('#')[0].Trim().TrimStart('\uFEFF'); if (line.Length == 0) continue;
            try
            {
                var pre = Regex.Match(line, @"^Preamp:\s*([-+\d.eE]+)\s*dB$", RegexOptions.IgnoreCase);
                if (pre.Success) { p.Preamp += Number(pre.Groups[1].Value); continue; }
                if (Regex.IsMatch(line, @"^(Device|Channel):\s*all$", RegexOptions.IgnoreCase)) continue;
                if (line.StartsWith("GraphicEQ:", StringComparison.OrdinalIgnoreCase))
                {
                    graphicCount++; if (graphicCount > 2) throw new FormatException(T("Mehr als zwei GraphicEQ-Stufen."));
                    var stage = graphicCount == 1 ? p.Graphic : p.Adjustment;
                    foreach (var part in line[10..].Split(';', StringSplitOptions.RemoveEmptyEntries))
                    { var nums = part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries); if (nums.Length != 2) throw new FormatException(T("Ungültiges Frequenz/Gain-Paar.")); stage.Add(new() { Frequency = Number(nums[0]), Gain = Number(nums[1]) }); }
                    if (stage.Count == 0) throw new FormatException(T("Leerer grafischer EQ.")); continue;
                }
                var m = Regex.Match(line, @"^Filter\s*\d*:\s*(ON|OFF)\s+(\w+)\s+Fc\s+([-+\d.eE]+)\s+Hz(?:\s+Gain\s+([-+\d.eE]+)\s+dB)?(?:\s+Q\s+([-+\d.eE]+))?$", RegexOptions.IgnoreCase);
                if (!m.Success) throw new FormatException(T("Nicht unterstützter Befehl: ") + line);
                var type = m.Groups[2].Value.ToUpperInvariant(); if (type == "PEQ") type = "PK";
                if (!Types.Contains(type)) throw new FormatException(F("Filtertyp {0} wird nicht unterstützt.", type));
                bool gainRequired = type is "PK" or "LS" or "HS" or "LSC" or "HSC";
                if (gainRequired != m.Groups[4].Success) throw new FormatException(T("Gain fehlt oder passt nicht zum Filtertyp."));
                if (type is "PK" or "AP" && !m.Groups[5].Success) throw new FormatException(T("Q fehlt."));
                p.Filters.Add(new() { Type = type, Enabled = m.Groups[1].Value.Equals("ON", StringComparison.OrdinalIgnoreCase), Frequency = Number(m.Groups[3].Value), Gain = m.Groups[4].Success ? Number(m.Groups[4].Value) : 0, Q = m.Groups[5].Success ? Number(m.Groups[5].Value) : Math.Sqrt(.5), UseQ = m.Groups[5].Success });
            }
            catch (Exception e) when (e is FormatException or OverflowException) { issues.Add(F("Zeile {0}: {1}", lineNo, e.Message)); }
        }
        try { Validate(p); } catch (InvalidDataException e) { issues.Add(e.Message); }
        if (p.Filters.Count == 0 && p.Graphic.Count == 0 && !text.Contains("Preamp:", StringComparison.OrdinalIgnoreCase)) issues.Add(T("Keine EQ-Einstellungen gefunden."));
        if (issues.Count > 0) throw new ImportException(issues);
        p.PresetOriginal = EqSettings.Capture(p); return p;
    }
}
