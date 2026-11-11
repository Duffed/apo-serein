using System.Diagnostics;
using System.Globalization;
using Serein.Core;

var root = args.FirstOrDefault(a => !a.StartsWith("--")) ?? Path.Combine(Path.GetTempPath(), "Serein-tests-" + Guid.NewGuid());
Directory.CreateDirectory(root);
int passed = 0, failed = 0;
var report = new List<string>();
void Assert(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
void Near(double actual, double expected, double tolerance = .001) => Assert(Math.Abs(actual - expected) < tolerance, $"{actual} != {expected}");
void Reject(Action action) { try { action(); } catch (Exception e) when (e is ImportException or InvalidDataException or IOException or UnauthorizedAccessException) { return; } throw new Exception("Expected rejection"); }
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; report.Add("PASS " + name); }
    catch (Exception ex) { failed++; report.Add("FAIL " + name + ": " + ex); }
}
Task Sync(Action a) { a(); return Task.CompletedTask; }
Profile Simple(double gain = 3) => ApoFormat.Import($"Preamp: -4 dB\nFilter 1: ON PK Fc 1000 Hz Gain {ApoFormat.N(gain)} dB Q 2\nFilter 2: OFF HSC Fc 8000 Hz Gain 2 dB Q 0.71", "Test");
ApoBackend Fixture(string label, string original = "# Original\r\nInclude: peace.txt\r\n")
{ var dir = Path.Combine(root, label); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "config.txt"), original); var b = new ApoBackend(dir); b.Setup(ApoBackend.FileHash(b.RootPath)); return b; }

await Test("APO numeric values, disabled filters and two graphic stages round-trip", () => Sync(() =>
{
    var p = Simple(); p.Graphic.Add(new() { Frequency = 20, Gain = 1 }); p.Graphic.Add(new() { Frequency = 20000, Gain = -2 }); p.Adjustment.Add(new() { Frequency = 1000, Gain = .5 });
    var q = ApoFormat.Import(ApoFormat.Export(p), "Again"); Near(q.Preamp, -4); Assert(q.Filters.Count == 2 && !q.Filters[1].Enabled); Near(q.Filters[0].Q, 2); Near(q.Graphic[1].Gain, -2); Near(q.Adjustment[0].Gain, .5);
}));
await Test("German culture exports decimal points", () => Sync(() => { var old = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = new("de-DE"); var p = Simple(2.3); Assert(ApoFormat.Export(p).Contains("Gain 2.3 dB")); } finally { CultureInfo.CurrentCulture = old; } }));
await Test("Unsupported APO commands and channels reject whole import", () => Sync(() => { foreach (var line in new[] { "Include: evil.txt", "Channel: L", "Device: Speakers", "VSTPlugin: test", "Convolution: test.wav", "Filter 2: ON BWLP Fc 100 Hz" }) Reject(() => ApoFormat.Import(ApoFormat.Export(Simple()) + line, "Bad")); }));
await Test("Invalid numbers, Q, unordered graphic points and missing gain rejected", () => Sync(() =>
{
    foreach (var text in new[] { "Preamp: NaN dB", "Filter 1: ON PK Fc 0 Hz Gain 1 dB Q 1", "Filter 1: ON PK Fc 1000 Hz Gain 1 dB Q 0", "Filter 1: ON PK Fc 1000 Hz Q 1", "GraphicEQ: 100 1; 20 0", "GraphicEQ: 100 1; 100 0", "GraphicEQ:" }) Reject(() => ApoFormat.Import(text, "Bad"));
}));
await Test("Peace shelf IDs, Q, preamp and disabled filters preserved", () => Sync(() =>
{
    var p = ApoFormat.Import("[General]\nPreAmp=-2.1\n[Frequencies]\nFrequency1=105\nFrequency2=10000\n[Gains]\nGain1=5.5\nGain2=-1\n[Qualities]\nQuality1=0.4\nQuality2=0.7\n[Filters]\nFilter1=14\nFilter2=5\n[Disabled]\nDisabled2=1", "Peace");
    Near(p.Preamp, -2.1); Assert(p.Filters[0].Type == "LSC" && p.Filters[0].UseQ); Near(p.Filters[0].Q, .4); Assert(p.Filters[1].Type == "HS" && !p.Filters[1].UseQ && !p.Filters[1].Enabled);
}));
await Test("Peace dormant channel defaults accepted, actual channel gains rejected", () => Sync(() =>
{
    var input = "[Frequencies]\nFrequency1=1000\n[Frequencies1]\nFrequency1=10\n[Qualities1]\nQuality1=1.41\n";
    Assert(ApoFormat.Import(input,"P").Filters.Count==1); Reject(() => ApoFormat.Import(input+"[Gains1]\nGain1=2", "P"));
}));
await Test("Peace unknown effects, filter variants and orphan settings rejected", () => Sync(() =>
{
    var input="[Frequencies]\nFrequency1=1000\n";
    foreach(var extra in new[]{"[Surround]\nEnable=1", "[General]\nDelay=5", "[Filters]\nFilter1=12", "[Gains]\nGain2=1"}) Reject(()=>ApoFormat.Import(input+extra,"Bad"));
}));
await Test("Existing Ananda, DT880 and Heco profiles import without loss", () => Sync(() =>
{
    var directory = @"C:\Program Files\EqualizerAPO\config";
    if (!Directory.Exists(directory)) { report.Add("NOTE local Peace fixtures unavailable"); return; }
    foreach (var file in Directory.GetFiles(directory, "#*.peace"))
    {
        var p=ApoFormat.Import(File.ReadAllText(file),Path.GetFileNameWithoutExtension(file));
        var q=ApoFormat.Import(ApoFormat.Export(p),p.Name); Assert(q.Filters.Count==p.Filters.Count); Near(q.Preamp,p.Preamp); report.Add($"  fixture {p.Name}: {p.Filters.Count} filters, preamp {p.Preamp}");
    }
}));
await Test("Known peak centre, disabled, notch and all-pass responses", () => Sync(() =>
{
    var f=new EqFilter{Frequency=1000,Gain=6,Q=2}; Near(Response.FilterAt(f,1000),6); f.Enabled=false; Near(Response.FilterAt(f,1000),0); f.Enabled=true; f.Type="NO"; Assert(Response.FilterAt(f,1000)<-100); f.Type="AP"; Near(Response.FilterAt(f,1500),0);
}));
await Test("Shelf centre/asymptotes and corner-vs-centre distinction", () => Sync(() =>
{
    var f=new EqFilter{Type="LSC",Frequency=1000,Gain=6,Q=.71}; Near(Response.FilterAt(f,1000),3); Near(Response.FilterAt(f,1),6,.01); Near(Response.FilterAt(f,20000),0,.01);
    f.Type="LS"; Assert(Math.Abs(Response.FilterAt(f,1000)-3)>.1); f.UseQ=false; Near(Response.FilterAt(f,1000),3);
}));
await Test("Graphic log interpolation and independent stages sum", () => Sync(() =>
{
    var p = new Profile { Preamp=-3 }; p.Graphic.Add(new(){Frequency=100,Gain=0}); p.Graphic.Add(new(){Frequency=10000,Gain=6}); p.Adjustment.Add(new(){Frequency=1000,Gain=2}); Near(Response.RawAt(p,1000)+p.Preamp,2);
}));
await Test("Automatic preamp adds margin and preserves existing lower preamp", () => Sync(() =>
{
    var p=Simple(6); p.Preamp=0; p.AutoHeadroom=true; Assert(Response.EffectivePreamp(p)<-6.19); p.Preamp=-20; Near(Response.EffectivePreamp(p),-20);
}));
await Test("Versioned persistence, profile cloning and unknown version protection", () => Sync(() =>
{
    var path=Path.Combine(root,"state.json"); var p=Simple(); var state=new AppState{Profiles=[p],Selected=p.Id,Devices=new(){["device"]=p.Id}}; ProfileStore.Save(path,state); var loaded=ProfileStore.Load(path); Assert(loaded.Devices["device"]==p.Id); var clone=p.Clone(); Assert(clone.Id!=p.Id); clone.Filters[0].Gain=99; Near(p.Filters[0].Gain,3); File.WriteAllText(path,"{\"Version\":99}"); Reject(()=>ProfileStore.Load(path));
}));
await Test("APO setup and byte-exact original restoration", () => Sync(() =>
{
    var b=Fixture("restore"); Assert(b.Check().Connected); Assert(!File.ReadAllText(b.RootPath).Contains("Include: peace.txt")); b.Restore(ApoBackend.FileHash(b.RootPath)); Assert(File.ReadAllText(b.RootPath)=="# Original\r\nInclude: peace.txt\r\n"); Assert(!b.Check().Connected);
}));
await Test("Unknown existing config and changed setup hash never overwritten", () => Sync(() =>
{
    var dir=Path.Combine(root,"unknown"); Directory.CreateDirectory(dir); var path=Path.Combine(dir,"config.txt"); File.WriteAllText(path,"Preamp: -10 dB"); var b=new ApoBackend(dir); Reject(()=>b.Setup(ApoBackend.FileHash(path))); Assert(File.ReadAllText(path)=="Preamp: -10 dB"); Reject(()=>b.Setup("bad hash"));
}));
await Test("Device-scoped output and bypass affect own EQ only", async () =>
{
    var b=Fixture("device"); var id="{0.0.0.00000000}.{11111111-1111-1111-1111-111111111111}"; await b.ApplyAsync(Simple(),id,false); Assert(File.ReadAllText(b.ActivePath).StartsWith("Device: {11111111-1111-1111-1111-111111111111}\n")); await b.ApplyAsync(Simple(),id,true); Assert(!File.ReadAllText(b.ActivePath).Contains("Filter")); Reject(()=>b.ApplyAsync(Simple(),"bad\nInclude: x",false));
});
await Test("APO device pattern matches device string without MMDevice prefix", () => Sync(() =>
{
    const string deviceGuid = "{22222222-2222-2222-2222-222222222222}";
    var endpoint = "{0.0.0.00000000}." + deviceGuid;
    var apoDeviceString = "USB Lautsprecher " + deviceGuid;
    Assert(!apoDeviceString.Contains(endpoint,StringComparison.OrdinalIgnoreCase));
    Assert(apoDeviceString.Contains(ApoBackend.DevicePattern(endpoint),StringComparison.OrdinalIgnoreCase));
    Assert(!"Realtek Lautsprecher {11111111-1111-1111-1111-111111111111}".Contains(ApoBackend.DevicePattern(endpoint),StringComparison.OrdinalIgnoreCase));
    Reject(()=>ApoBackend.DevicePattern("{0.0.0.00000000}.{---}"));
}));
await Test("Rapid selections are serialized; final selection wins", async () =>
{
    var b=Fixture("rapid"); var tasks=Enumerable.Range(0,100).Select(i=>b.ApplyAsync(Simple(i/10.0),null,false)).ToList(); await Task.WhenAll(tasks); Assert(File.ReadAllText(b.ActivePath).Contains("Gain 9.9 dB")); Assert(b.Check().Connected);
});
await Test("External root and active-file edits halt updates", async () =>
{
    var b=Fixture("conflict"); File.AppendAllText(b.RootPath,"# external"); try{await b.ApplyAsync(Simple(),null,false);throw new Exception("No conflict");}catch(IOException){} Assert(File.ReadAllText(b.RootPath).Contains("# external")); Reject(()=>b.Restore(ApoBackend.FileHash(b.RootPath)));
    var c=Fixture("active-conflict"); File.WriteAllText(c.ActivePath,"# external"); try{await c.ApplyAsync(Simple(),null,false);throw new Exception("No conflict");}catch(IOException){} Assert(File.ReadAllText(c.ActivePath)=="# external");
});
await Test("Locked output fails without corrupting active configuration", async () =>
{
    var b=Fixture("locked"); var before=File.ReadAllText(b.ActivePath); using(var locked=new FileStream(b.ActivePath,FileMode.Open,FileAccess.Read,FileShare.None)){try{await b.ApplyAsync(Simple(),null,false);throw new Exception("Expected lock failure");}catch(IOException){}} Assert(File.ReadAllText(b.ActivePath)==before); Assert(b.Check().Connected);
});
await Test("AutoEQ index parses escaped paths; cache supports offline download", async () =>
{
    var seed=Path.Combine(AppContext.BaseDirectory,"autoeq-index.md");
    var catalog=new AutoEqCatalog(Path.Combine(root,"catalog")); catalog.Load(seed); Assert(catalog.Entries.Count>1000,"Seed index missing");
    var entry=catalog.Entries.First(e=>e.Name.Contains("Ananda",StringComparison.OrdinalIgnoreCase)); var key=ApoBackend.Hash(System.Text.Encoding.UTF8.GetBytes(entry.Path)); AtomicFile.Write(Path.Combine(root,"catalog",key+".txt"),ApoFormat.Export(Simple()));
    var p=await catalog.DownloadAsync(entry); Assert(p.Filters.Count==2 && p.Source==entry.SourceUrl); Assert(!AutoEqCatalog.EncodePath("a/test (x)").Contains(' '));
});
await Test("Device changes restore stored profiles; new defaults start neutral", () => Sync(() =>
{
    var p=Simple(); var q=Simple(6); var profiles=new List<Profile>{p,q}; var state=new AppState{Devices=new(){["headphone"]=p.Id,["speaker"]=q.Id}};
    Assert(DeviceProfiles.Resolve(state,profiles,"speaker","Speakers",p,true)==q);
    Assert(DeviceProfiles.Resolve(state,profiles,"headphone","Headphones",q,false)==p);
    var neutral=DeviceProfiles.Resolve(state,profiles,"new","HDMI",q,true); Assert(neutral.Filters.Count==0 && neutral.Preamp==0 && neutral.Id!=q.Id);
    Assert(DeviceProfiles.Resolve(state,profiles,"new","HDMI",q,true)==neutral && profiles.Count==3);
    Assert(DeviceProfiles.Resolve(state,profiles,"manual","X7",p,false)==p);
}));
await Test("Malformed catalog cache falls back to bundled index", () => Sync(() =>
{
    var path=Path.Combine(root,"bad-catalog"); Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path,"index.md"),"invalid cache");
    var catalog=new AutoEqCatalog(path); catalog.Load(Path.Combine(AppContext.BaseDirectory,"autoeq-index.md")); Assert(catalog.Entries.Count>1000);
}));
await Test("Read-only root setup preserves original and removes incomplete manifest", () => Sync(() =>
{
    var dir=Path.Combine(root,"read-only"); Directory.CreateDirectory(dir); var rootPath=Path.Combine(dir,"config.txt"); File.WriteAllText(rootPath,"Include: peace.txt"); File.SetAttributes(rootPath,FileAttributes.ReadOnly);
    try { var b=new ApoBackend(dir); Reject(()=>b.Setup(ApoBackend.FileHash(rootPath))); Assert(File.ReadAllText(rootPath)=="Include: peace.txt"); Assert(!File.Exists(Path.Combine(b.OwnDirectory,"setup.json"))); }
    finally { File.SetAttributes(rootPath,FileAttributes.Normal); }
}));
await Test("Atomic writes never expose partial configurations", async () =>
{
    var b=Fixture("atomic"); var texts=Enumerable.Range(0,60).Select(i=>ApoFormat.Export(Simple(i/10.0))).ToHashSet(); var original=File.ReadAllText(b.ActivePath); bool done=false;
    var reader=Task.Run(()=> { while(!Volatile.Read(ref done)) { try { using var stream=new FileStream(b.ActivePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete); using var input=new StreamReader(stream); var text=input.ReadToEnd(); Assert(text==original || texts.Contains(text),"Partial configuration observed"); }catch(IOException){} } });
    try { for(int i=0;i<60;i++) await b.ApplyAsync(Simple(i/10.0),null,false); }
    finally { Volatile.Write(ref done,true); await reader; }
});
await Test("Local file-write latency, 100 switches", async () =>
{
    var b=Fixture("perf"); var p=Simple(); var times=new List<double>();
    for(int i=0;i<100;i++){p.Preamp=-i/10.0;var sw=Stopwatch.StartNew();await b.ApplyAsync(p,null,false);times.Add(sw.Elapsed.TotalMilliseconds);}
    times.Sort(); report.Add($"  write latency p50 {times[49]:F2} ms, p95 {times[94]:F2} ms, max {times[^1]:F2} ms"); Assert(times[94]<100,"p95 above 100 ms");
});
if(args.Contains("--online")) await Test("Live AutoEQ download and validation", async()=>
{
    var catalog=new AutoEqCatalog(Path.Combine(root,"online")); catalog.Load(Path.Combine(AppContext.BaseDirectory,"autoeq-index.md")); var entry=catalog.Entries.First(e=>e.Name=="Sennheiser HD 600"); var p=await catalog.DownloadAsync(entry); Assert(p.Filters.Count>0 || p.Graphic.Count>0); report.Add($"  {p.Name}: {p.Filters.Count} filters, {p.Graphic.Count} graphic points");
});
await Test("Peace migration preserves selection, detects duplicates and reports unsupported files", () => Sync(() =>
{
    var directory=Path.Combine(root,"peace-migration"); Directory.CreateDirectory(directory);
    var text="[General]\nDescription=Original preset notes\nPreAmp=-2\n[Frequencies]\nFrequency1=1000\n[Gains]\nGain1=3";
    File.WriteAllText(Path.Combine(directory,"Personal.peace"),text);
    File.WriteAllText(Path.Combine(directory,"Unsupported.peace"),text+"\n[Surround]\nEnable=1");
    var original=Simple(); var state=new AppState{Profiles=[original],Selected=original.Id,Devices=new(){["X7"]=original.Id}};
    var result=PeaceMigration.Import(state,directory); Assert(result.Added==1 && result.Issues.Count==1); Assert(state.Selected==original.Id && state.Devices["X7"]==original.Id);
    var imported=state.Profiles.Single(p=>p.Name=="Personal"); Assert(imported.SourceSummary=="Peace · lokal" && imported.Notes!.Contains("Original preset notes"));
    imported.Preamp=-9; File.WriteAllText(Path.Combine(directory,"Personal.peace"),text.Replace("PreAmp=-2","PreAmp=-5"));
    var again=PeaceMigration.Import(state,directory); Assert(again.Added==0 && again.Existing==1); Near(imported.Preamp,-9);
    var statePath=Path.Combine(root,"migration-state.json"); state.PeaceMigrationVersion=1; ProfileStore.Save(statePath,state); var loaded=ProfileStore.Load(statePath); Assert(loaded.PeaceMigrationVersion==1 && loaded.Profiles[1].SourceFile==imported.SourceFile);
}));
await Test("Source labels work for old AutoEQ profiles and metadata persists", () => Sync(() =>
{
    var p=Simple(); p.Source="https://github.com/jaakkopasanen/AutoEq/tree/master/results/oratory1990/over-ear/Model"; Assert(p.SourceSummary=="AutoEQ · oratory1990");
    p.Notes="Published notes"; p.TargetCurve="Explicit target"; var clone=p.Clone(); Assert(clone.Notes==p.Notes && clone.TargetCurve==p.TargetCurve);
}));
await Test("Bulk migration includes all personal Peace fixtures", () => Sync(() =>
{
    const string directory=@"C:\Program Files\EqualizerAPO\config"; if(!Directory.Exists(directory))return;
    var state=new AppState(); var result=PeaceMigration.Import(state,directory);
    foreach(var path in Directory.GetFiles(directory,"#*.peace")) Assert(state.Profiles.Any(p=>p.SourceFile==Path.GetFullPath(path)),"Missing personal profile: "+path);
    report.Add($"  Peace migration: {result.Added} imported, {result.Issues.Count} unsupported (reported, not partially imported)");
}));
await Test("AutoEQ notes and explicit target metadata without invented values", () => Sync(() =>
{
    var entry=new CatalogEntry("Test","measurement/over-ear/Test","measurement");
    var info=AutoEqCatalog.ParseInfo(entry,"# Test\nSee [usage instructions](https://example.com).\n### Parametric EQs\nUse filters 1-5. Apply **-5 dB** preamp.\n| # | Type |\n| 1 | Peak |\n### Graphs\n![](graph.png)");
    Assert(info.TargetCurve==null && info.Notes.Contains("Use filters 1-5") && !info.Notes.Contains("| 1 |") && !info.Notes.Contains("graph.png"));
    Assert(AutoEqCatalog.ParseInfo(entry,"Target curve: Harman 2018\nAn explicit note").TargetCurve=="Harman 2018");
}));
await Test("Cached AutoEQ metadata is available offline", async () =>
{
    var path=Path.Combine(root,"metadata-cache"); var entry=new CatalogEntry("Test","measurement/over-ear/Test","measurement");
    var key=ApoBackend.Hash(System.Text.Encoding.UTF8.GetBytes(entry.Path)); AtomicFile.Write(Path.Combine(path,key+".md"),"### Parametric EQs\nOriginal offline instructions");
    var catalog=new AutoEqCatalog(path); var info=await catalog.GetInfoAsync(entry); Assert(info.Notes.Contains("Original offline instructions") && info.Measurement=="measurement");
});
if(args.Contains("--online")) await Test("Live AutoEQ profile metadata", async () =>
{
    var catalog=new AutoEqCatalog(Path.Combine(root,"live-metadata")); var entry=new CatalogEntry("HIFIMAN Ananda","oratory1990/over-ear/HIFIMAN%20Ananda","oratory1990");
    var info=await catalog.GetInfoAsync(entry); Assert(info.Notes.Contains("Parametric EQs") && info.Notes.Length>50); report.Add("  AutoEQ published instructions loaded and cached");
});
report.Add($"RESULT {passed} passed, {failed} failed");
File.WriteAllLines(Path.Combine(root,"test-report.txt"),report); foreach(var line in report) Console.WriteLine(line); return failed==0?0:1;
