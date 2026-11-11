using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Serein.Core;

public record BackendStatus(bool Available, bool Connected, string Message);
public record ApplyResult(bool Written, double Milliseconds, string Message);
public interface IAudioBackend
{
    BackendStatus Check();
    Task<ApplyResult> ApplyAsync(Profile profile, string? deviceId, bool bypass);
}
public class SetupManifest
{
    public int Version { get; set; } = 1;
    public string OriginalBase64 { get; set; } = "";
    public string ManagedRootHash { get; set; } = "";
    public string ActiveHash { get; set; } = "";
}
public sealed class ApoBackend(string directory) : IAudioBackend
{
    readonly SemaphoreSlim gate = new(1, 1);
    long revision;
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public string RootPath => Path.Combine(DirectoryPath, "config.txt");
    public string OwnDirectory => Path.Combine(DirectoryPath, "Serein");
    public string ActivePath => Path.Combine(OwnDirectory, "active.txt");
    string ManifestPath => Path.Combine(OwnDirectory, "setup.json");
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string FileHash(string path) => File.Exists(path) ? Hash(File.ReadAllBytes(path)) : "";
    public static string DevicePattern(string endpointId)
    {
        // MMDevice endpoint IDs are persistence keys, not APO device patterns.
        // APO matches against "device name connection name {endpoint GUID}".
        var match = Regex.Match(endpointId, @"^\{0\.0\.0\.\d+\}\.\{([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\}$");
        if (!match.Success) throw new InvalidDataException("Ungültige Windows-Gerätekennung.");
        return "{" + match.Groups[1].Value + "}";
    }
    SetupManifest? Manifest() => File.Exists(ManifestPath) ? JsonSerializer.Deserialize<SetupManifest>(File.ReadAllText(ManifestPath)) : null;
    public BackendStatus Check()
    {
        try
        {
            if (!File.Exists(RootPath)) return new(false, false, "APO-Konfiguration nicht gefunden.");
            var m = Manifest();
            if (m == null) return new(true, false, "Vorschau · APO noch nicht verbunden");
            bool valid = m.ManagedRootHash == FileHash(RootPath) && m.ActiveHash == FileHash(ActivePath);
            return new(true, valid, valid ? "APO verbunden" : "APO-Konfiguration geändert · zum Übernehmen erneut verbinden");
        }
        catch (Exception e) { return new(false, false, e.Message); }
    }
    public void Setup(string expectedRootHash, bool reconnect = false)
    {
        if (FileHash(RootPath) != expectedRootHash) throw new IOException("Die APO-Konfiguration wurde inzwischen geändert.");
        SetupManifest? previous = null;
        if (File.Exists(ManifestPath))
        {
            if (!reconnect) { if (!Check().Connected) throw new IOException("APO wurde zwischenzeitlich geändert. Bitte „Neu verbinden“ verwenden."); return; }
            previous = Manifest();
        }
        var original = File.ReadAllBytes(RootPath);
        var text = File.ReadAllText(RootPath);
        var lines = text.Split('\n'); int peaceIncludes = 0; bool ownInclude = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (Regex.IsMatch(line, "^Include:\\s*\"?peace\\.txt\"?\\s*$", RegexOptions.IgnoreCase)) { peaceIncludes++; continue; }
            if (reconnect && Regex.IsMatch(line, "^Include:\\s*\"?Serein[/\\\\]active\\.txt\"?\\s*$", RegexOptions.IgnoreCase)) { ownInclude = true; continue; }
            throw new InvalidDataException("Die Hauptkonfiguration enthält weitere Befehle. Automatische Einrichtung unterstützt eine leere oder reine Peace-Konfiguration; vorhandene Befehle werden nicht überschrieben.");
        }
        if (peaceIncludes > 1) throw new InvalidDataException("Mehrfache Peace-Einbindung gefunden.");
        Directory.CreateDirectory(OwnDirectory);
        if (reconnect)
        {
            var backup = Path.Combine(OwnDirectory, "backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..6]);
            Directory.CreateDirectory(backup); File.WriteAllBytes(Path.Combine(backup, "config.txt"), original);
            if (File.Exists(ActivePath)) File.Copy(ActivePath, Path.Combine(backup, "active.txt"));
            if (File.Exists(ManifestPath)) File.Copy(ManifestPath, Path.Combine(backup, "setup.json"));
            // Reconnecting our own root must not turn the restore backup into a self-include.
            if (previous != null && ownInclude && peaceIncludes == 0) original = Convert.FromBase64String(previous.OriginalBase64);
        }
        if (FileHash(RootPath) != expectedRootHash) throw new IOException("APO wurde während der Einrichtung geändert. Bitte Peace beenden und erneut verbinden.");
        var priorManifestText = File.Exists(ManifestPath) ? File.ReadAllText(ManifestPath) : null;
        var priorActiveText = File.Exists(ActivePath) ? File.ReadAllText(ActivePath) : null;
        var managed = "# Serein – Original in Serein/setup.json gesichert\nInclude: Serein/active.txt\n";
        var active = "# Serein: eingerichtet, eigener EQ umgangen\n";
        AtomicFile.Write(ActivePath, active);
        var manifest = new SetupManifest { OriginalBase64 = Convert.ToBase64String(original), ManagedRootHash = Hash(Encoding.UTF8.GetBytes(managed)), ActiveHash = FileHash(ActivePath) };
        AtomicFile.Write(ManifestPath, JsonSerializer.Serialize(manifest, ProfileStore.Json));
        try { AtomicFile.Write(RootPath, managed); }
        catch
        {
            if (priorManifestText == null) File.Delete(ManifestPath); else AtomicFile.Write(ManifestPath, priorManifestText);
            if (priorActiveText != null) AtomicFile.Write(ActivePath, priorActiveText);
            throw;
        }
    }
    public async Task WaitForIdleAsync() { await gate.WaitAsync(); gate.Release(); }
    public void Restore(string expectedRootHash)
    {
        var m = Manifest() ?? throw new IOException("Keine Sicherung vorhanden.");
        if (FileHash(RootPath) != expectedRootHash || expectedRootHash != m.ManagedRootHash) throw new IOException("Externe Änderung der Hauptkonfiguration. Wiederherstellung wurde angehalten.");
        var temp = RootPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, Convert.FromBase64String(m.OriginalBase64)); File.Move(temp, RootPath, true); File.Delete(ManifestPath); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public Task<ApplyResult> ApplyAsync(Profile profile, string? deviceId, bool bypass)
    {
        // Serialize on caller's thread so a pending slider edit cannot change the queued snapshot.
        var content = ApoFormat.Export(profile, bypass);
        var pattern = deviceId == null ? null : DevicePattern(deviceId);
        if (!bypass && pattern != null) content = "Device: " + pattern + "\nChannel: all\n" + content;
        var request = Interlocked.Increment(ref revision);
        return Task.Run(async () =>
        {
            await gate.WaitAsync();
            try
            {
                if (request != Volatile.Read(ref revision)) return new ApplyResult(false, 0, "Neuere Auswahl vorgemerkt");
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var status = Check(); if (!status.Connected) throw new IOException(status.Message);
                var m = Manifest()!;
                AtomicFile.Write(ActivePath, content);
                m.ActiveHash = FileHash(ActivePath);
                AtomicFile.Write(ManifestPath, JsonSerializer.Serialize(m, ProfileStore.Json));
                return new ApplyResult(true, watch.Elapsed.TotalMilliseconds, "Konfiguration angewendet");
            }
            finally { gate.Release(); }
        });
    }
}
