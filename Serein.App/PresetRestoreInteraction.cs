using System.IO;
using System.Windows;
using System.Windows.Controls;
using Serein.Core;

namespace Serein.App;

public partial class MainWindow
{
    readonly HashSet<Guid> recoveringOriginals = [];
    PresetSources? presetSources;
    string? presetConfig;
    bool restoringPreset;
    PresetSources Sources
    {
        get
        {
            if (presetSources == null || presetConfig != state.ConfigDirectory)
            { presetConfig = state.ConfigDirectory; presetSources = new(Path.Combine(dataDirectory, "autoeq"), Path.Combine(AppContext.BaseDirectory, "autoeq-index.md"), state.ConfigDirectory); }
            return presetSources;
        }
    }
    void UpdatePresetButton()
    {
        if (current == null) return;
        bool changed = current.PresetOriginal is EqSettings original ? !original.Same(EqSettings.Capture(current), true) : PresetSources.CanRecover(current);
        PresetResetButton.Visibility = changed ? Visibility.Visible : Visibility.Hidden;
        PresetResetButton.IsEnabled = !restoringPreset;
        PresetResetButton.ToolTip = current.PresetOriginal != null ? "Ursprüngliche EQ-Werte laden. Mit Rückgängig lassen sich die Änderungen zurückholen." : "Original-Preset aus der Datei oder AutoEQ laden.";
    }
    async Task RememberPresetOriginal(Profile p)
    {
        if (p.PresetOriginal != null || !PresetSources.CanRecover(p) || !recoveringOriginals.Add(p.Id)) return;
        try
        {
            var sources = Sources; var original = await Task.Run(() => sources.ReadLocal(p));
            if (original != null && p.PresetOriginal == null) { original.Validate(); p.PresetOriginal = original; SaveState(); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ImportException or ArgumentException) { /* An explicit restore reports unavailable/unsupported sources. */ }
        finally { recoveringOriginals.Remove(p.Id); if (ReferenceEquals(current, p)) UpdatePresetButton(); }
    }
    async void RestorePreset(object sender, RoutedEventArgs e)
    {
        if (current == null || restoringPreset) return;
        Curve.CancelEdit(); var profile = current; restoringPreset = true; UpdatePresetButton();
        try
        {
            var original = profile.PresetOriginal ?? await Sources.LoadAsync(profile); original.Validate();
            profile.PresetOriginal = original;
            if (!ReferenceEquals(current, profile)) { SaveState(); return; }
            if (!FilterGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !FilterGrid.CommitEdit(DataGridEditingUnit.Row, true))
                throw new InvalidDataException("Bitte zuerst die ungültige Filtereingabe korrigieren.");
            ApoFormat.Validate(profile); editTimer.Stop(); var history = HistoryFor(profile); history.Begin();
            updating = true;
            try
            {
                original.Restore(profile);
                if (profile.Adjustment.Count == 0) foreach (var hz in Response.BandFrequencies(10)) profile.Adjustment.Add(new() { Frequency = hz });
                AttachProfile();
            }
            finally { updating = false; history.End(); }
            Curve.Profile = null; Populate(); await CommitChanges();
        }
        catch (Exception ex) { Error(ex); }
        finally { restoringPreset = false; UpdateHistoryButtons(); }
    }
}
