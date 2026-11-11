using static Serein.Core.UiLanguage;
using System.Text.RegularExpressions;

namespace Serein.Core;

public record CatalogEntry(string Name, string Path, string Measurement)
{
    public string Category => Uri.UnescapeDataString(Path.Split('/').Length > 1 ? Path.Split('/')[1] : "");
    public string Description => "AutoEQ · " + Measurement + " · " + Category;
    public string SourceUrl => "https://github.com/jaakkopasanen/AutoEq/tree/master/results/" + AutoEqCatalog.EncodePath(Path);
}
public record CatalogInfo(string Measurement, string Category, string? TargetCurve, string Notes);
public sealed class AutoEqCatalog(string cacheDirectory)
{
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(25) };
    public List<CatalogEntry> Entries { get; private set; } = [];
    public static string EncodePath(string path) => string.Join('/', path.Split('/').Select(p => Uri.EscapeDataString(Uri.UnescapeDataString(p))));
    public void Load(string seed)
    {
        var cache = Path.Combine(cacheDirectory, "index.md");
        if (File.Exists(cache))
        {
            try { var entries = Parse(File.ReadAllText(cache)); if (entries.Count >= 100) { Entries = entries; return; } }
            catch (IOException) { /* keep packaged index available */ }
        }
        if (File.Exists(seed)) Entries = Parse(File.ReadAllText(seed));
    }
    public static List<CatalogEntry> Parse(string text) => text.Split('\n').Select(line => Regex.Match(line.Trim(), @"^- \[(.+)\]\(\./(.+)\)$"))
        .Where(m => m.Success).Select(m => new CatalogEntry(m.Groups[1].Value, m.Groups[2].Value, Uri.UnescapeDataString(m.Groups[2].Value.Split('/')[0]))).DistinctBy(e => e.Path).ToList();
    public async Task RefreshAsync()
    {
        var text = await GetText("https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/results/README.md");
        var entries = Parse(text); if (entries.Count < 100) throw new InvalidDataException(T("AutoEQ-Index unvollständig; bisheriger Cache bleibt erhalten."));
        AtomicFile.Write(Path.Combine(cacheDirectory, "index.md"), text); Entries = entries;
    }
    public async Task<Profile> DownloadAsync(CatalogEntry entry)
    {
        var key = ApoBackend.Hash(System.Text.Encoding.UTF8.GetBytes(entry.Path));
        var cache = Path.Combine(cacheDirectory, key + ".txt"); string text;
        if (File.Exists(cache)) text = File.ReadAllText(cache);
        else
        {
            var path = EncodePath(entry.Path); var model = Uri.UnescapeDataString(entry.Path.Split('/')[^1]);
            var url = "https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/results/" + path + "/";
            try { text = await GetText(url + Uri.EscapeDataString(model + " ParametricEQ.txt")); }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { text = await GetText(url + Uri.EscapeDataString(model + " GraphicEQ.txt")); }
            var valid = ApoFormat.Import(text, entry.Name); // validate before caching
            AtomicFile.Write(cache, text);
        }
        var profile = ApoFormat.Import(text, entry.Name); profile.Source = entry.SourceUrl; profile.MeasurementSource = entry.Measurement; profile.Category = entry.Category;
        return profile;
    }
    public async Task<CatalogInfo> GetInfoAsync(CatalogEntry entry)
    {
        var key = ApoBackend.Hash(System.Text.Encoding.UTF8.GetBytes(entry.Path));
        var cache = Path.Combine(cacheDirectory, key + ".md"); string text;
        if (File.Exists(cache)) text = File.ReadAllText(cache);
        else
        {
            try { text = await GetText("https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/results/" + EncodePath(entry.Path) + "/README.md"); }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return new(entry.Measurement, entry.Category, null, T("Keine zusätzlichen Hinweise veröffentlicht.")); }
            AtomicFile.Write(cache, text);
        }
        return ParseInfo(entry, text);
    }
    public static CatalogInfo ParseInfo(CatalogEntry entry, string markdown)
    {
        var notes = new List<string>(); string? target = null;
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('|') || line.StartsWith("![") || line.StartsWith("# ") || line.StartsWith("See [usage instructions]")) continue;
            var targetMatch = Regex.Match(line, @"^(?:[-*]\s*)?(?:\*\*)?(?:Target(?: curve)?|Zielkurve)(?:\*\*)?\s*:\s*(.+)$", RegexOptions.IgnoreCase);
            if (targetMatch.Success) target = Regex.Replace(targetMatch.Groups[1].Value, @"\*\*", "");
            if (line.StartsWith('#')) { if (line.Contains("Graphs", StringComparison.OrdinalIgnoreCase)) break; line = line.TrimStart('#', ' '); }
            line = Regex.Replace(line, @"\[([^\]]+)\]\([^)]+\)", "$1").Replace("**", "").Replace("`", "");
            notes.Add(line);
            if (notes.Sum(n => n.Length) > 5000) break;
        }
        return new(entry.Measurement, entry.Category, target, notes.Count == 0 ? T("Keine zusätzlichen Hinweise veröffentlicht.") : string.Join("\n\n", notes));
    }
    static async Task<string> GetText(string url)
    {
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead); response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 10_000_000) throw new InvalidDataException(T("Download zu groß."));
        using var stream = await response.Content.ReadAsStreamAsync(); using var output = new MemoryStream(); var buffer = new byte[8192]; int n;
        while ((n = await stream.ReadAsync(buffer)) > 0) { if (output.Length + n > 10_000_000) throw new InvalidDataException(T("Download zu groß.")); output.Write(buffer, 0, n); }
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }
}
