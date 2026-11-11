namespace Serein.Core;

public record PeaceImportResult(int Added, int Existing, IReadOnlyList<string> Issues);
public static class PeaceMigration
{
    public static PeaceImportResult Import(AppState state, string directory)
    {
        int added = 0, existing = 0; var issues = new List<string>();
        if (!Directory.Exists(directory)) return new(0, 0, ["Peace-Konfigurationsordner nicht gefunden: " + directory]);
        string[] files;
        try { files = Directory.GetFiles(directory, "*.peace"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(0, 0, ["Peace-Ordner konnte nicht gelesen werden: " + ex.Message]); }
        foreach (var path in files.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = Path.GetFullPath(path);
            var name = Path.GetFileNameWithoutExtension(path);
            if (state.SuppressedPeaceFiles.Contains(fullPath, StringComparer.OrdinalIgnoreCase)) { existing++; continue; }
            if (state.Profiles.Any(p => string.Equals(p.SourceFile, fullPath, StringComparison.OrdinalIgnoreCase) ||
                (p.Name == name && p.Source == "Import: " + Path.GetFileName(path)))) { existing++; continue; }
            try
            {
                if (new FileInfo(path).Length > 2_000_000) throw new InvalidDataException("Datei zu groß.");
                var profile = ApoFormat.Import(File.ReadAllText(path), name);
                profile.Source = "Import: " + Path.GetFileName(path); profile.SourceFile = fullPath;
                profile.Notes = (string.IsNullOrWhiteSpace(profile.Notes) ? "" : profile.Notes + "\n\n") + "Aus deiner lokalen Peace-Konfiguration übernommen. Die Originaldatei bleibt unverändert.";
                state.Profiles.Add(profile); added++;
            }
            catch (Exception ex) when (ex is ImportException or IOException or UnauthorizedAccessException or InvalidDataException)
            { issues.Add(Path.GetFileName(path) + ": " + ex.Message); }
        }
        return new(added, existing, issues);
    }
}
