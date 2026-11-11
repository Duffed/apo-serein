using System.IO;
using System.Security.Principal;
using System.Diagnostics;
using Serein.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Serein.App;
public partial class App : Application
{
    Mutex? singleton;
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0 && e.Args[0] is "--setup" or "--restore" or "--reconnect")
        {
            try
            {
                if (e.Args.Length != 4) throw new InvalidDataException("Ungültiger Einrichtungsaufruf.");
                var root = Path.GetFullPath(e.Args[1]);
                if (!File.Exists(Path.Combine(Path.GetDirectoryName(root)!, "EqualizerAPO.dll"))) throw new InvalidDataException("Kein Equalizer-APO-Konfigurationsordner.");
                var backend = new ApoBackend(root);
                if (e.Args[0] == "--restore") backend.Restore(e.Args[2]);
                else
                {
                    var sid = new SecurityIdentifier(e.Args[3]);
                    backend.Setup(e.Args[2], e.Args[0] == "--reconnect");
                    // Grant only the app-owned subdirectory; never the APO root or other users.
                    var start = new ProcessStartInfo("icacls.exe") { UseShellExecute = false, CreateNoWindow = true };
                    start.ArgumentList.Add(backend.OwnDirectory); start.ArgumentList.Add("/grant"); start.ArgumentList.Add("*" + sid.Value + ":(OI)(CI)M");
                    using var process = Process.Start(start)!; process.WaitForExit(); if (process.ExitCode != 0) throw new IOException("Schreibrechte konnten nicht eingerichtet werden.");
                }
                Shutdown(0);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Serein · Einrichtung"); Shutdown(1); }
            return;
        }
        if (!e.Args.Contains("--smoke") && !e.Args.Contains("--ui-test"))
        {
            singleton = new Mutex(true, "Local\\Serein-" + WindowsIdentity.GetCurrent().User!.Value, out var created);
            if (!created) { MessageBox.Show("Serein läuft bereits. Öffne das Fenster über das Symbol im Systemtray.", "Serein"); Shutdown(); return; }
        }
        DispatcherUnhandledException += (_, args) => { MessageBox.Show(args.Exception.Message, "Serein"); args.Handled = true; };
        try { new MainWindow(e.Args.Contains("--smoke") || e.Args.Contains("--ui-test"), e.Args.Contains("--ui-test")).Show(); }
        catch (Exception ex) { MessageBox.Show("Start fehlgeschlagen: " + ex.Message + "\nProfildateien wurden nicht überschrieben.", "Serein"); Shutdown(1); }
    }
    protected override void OnExit(System.Windows.ExitEventArgs e) { singleton?.Dispose(); base.OnExit(e); }
}
