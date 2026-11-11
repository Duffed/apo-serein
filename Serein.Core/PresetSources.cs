using static Serein.Core.UiLanguage;
using System.Text;

namespace Serein.Core;

public sealed class PresetSources(string cacheDirectory, string indexPath, string configDirectory)
{
    const string AutoEqPrefix = "https://github.com/jaakkopasanen/AutoEq/tree/master/results/";
    readonly AutoEqCatalog catalog = new(cacheDirectory);
    readonly object catalogGate = new();
    bool loaded;
    public static bool CanRecover(Profile p) => !string.IsNullOrWhiteSpace(p.SourceFile) || p.Source?.StartsWith("Import: ") == true || p.Source?.StartsWith(AutoEqPrefix, StringComparison.OrdinalIgnoreCase) == true;
    CatalogEntry? Entry(Profile p)
    {
        if (p.Source?.StartsWith(AutoEqPrefix, StringComparison.OrdinalIgnoreCase) != true) return null;
        lock (catalogGate)
        {
            if (!loaded) { catalog.Load(indexPath); loaded = true; }
            return catalog.Entries.FirstOrDefault(e => string.Equals(e.SourceUrl.TrimEnd('/'), p.Source.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
        }
    }
    static EqSettings Read(string path, string name)
    {
        if (new FileInfo(path).Length > 2_000_000) throw new InvalidDataException(T("Original-Preset ist zu groß."));
        return EqSettings.Capture(ApoFormat.Import(File.ReadAllText(path), name));
    }
    public EqSettings? ReadLocal(Profile p)
    {
        if (p.PresetOriginal != null) return p.PresetOriginal;
        if (!string.IsNullOrWhiteSpace(p.SourceFile) && File.Exists(p.SourceFile)) return Read(p.SourceFile, p.Name);
        string? file = p.Source?.StartsWith("Import: ") == true ? p.Source[8..] : p.SourceFile == null ? null : Path.GetFileName(p.SourceFile);
        if (!string.IsNullOrWhiteSpace(file) && Path.GetFileName(file) == file && File.Exists(Path.Combine(configDirectory, file))) return Read(Path.Combine(configDirectory, file), p.Name);
        if (Entry(p) is CatalogEntry entry)
        {
            var cache = Path.Combine(cacheDirectory, ApoBackend.Hash(Encoding.UTF8.GetBytes(entry.Path)) + ".txt");
            if (File.Exists(cache)) return Read(cache, p.Name);
        }
        return null;
    }
    public async Task<EqSettings> LoadAsync(Profile p)
    {
        var local = await Task.Run(() => ReadLocal(p)); if (local != null) return local;
        if (Entry(p) is CatalogEntry entry) return EqSettings.Capture(await catalog.DownloadAsync(entry));
        throw new IOException(T("Das ursprüngliche Preset ist nicht mehr verfügbar. Die Originaldatei erneut importieren oder das Preset aus dem Katalog laden."));
    }
}
