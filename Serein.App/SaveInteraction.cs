using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Serein.Core;

namespace Serein.App;

public partial class MainWindow
{
    readonly Dictionary<Guid, EqSettings> savedEq = [];
    TextBox? pendingFilterEditor;
    string? pendingFilterProperty;
    double pendingFilterValue;
    static bool ReadNumber(string text, out double value) => double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    void MarkProfilesSaved()
    {
        savedEq.Clear(); foreach (var p in profiles) savedEq[p.Id] = EqSettings.Capture(p);
        UpdateSaveButton();
    }
    void UpdateSaveButton()
    {
        if (current == null || SaveButton == null) return;
        bool changed = !savedEq.TryGetValue(current.Id, out var saved) || !saved.Same(EqSettings.Capture(current));
        bool valid = true;
        if (PreampValue.IsKeyboardFocusWithin)
        {
            valid = ReadNumber(PreampValue.Text, out var value) && Math.Abs(value) <= 100;
            changed |= valid && value != current.Preamp;
        }
        if (pendingFilterEditor is TextBox editor)
        {
            bool inputValid = ReadNumber(editor.Text, out var value) && pendingFilterProperty switch
            { "Frequency" => value >= 1 && value <= 24000, "Gain" => Math.Abs(value) <= 100, "Q" => value >= .01 && value <= 100, _ => true };
            valid &= inputValid; changed |= inputValid && value != pendingFilterValue;
        }
        SaveButton.IsEnabled = valid && changed;
        SaveButton.ToolTip = !valid ? T("Eingabe prüfen") : changed ? T("Änderungen speichern") : T("Alle Änderungen gespeichert");
    }
    void PendingPreampChanged(object sender, TextChangedEventArgs e) { if (!updating) UpdateSaveButton(); }
    void PrepareFilterEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (pendingFilterEditor != null) pendingFilterEditor.Unloaded -= FilterEditorClosed;
        pendingFilterEditor = e.EditingElement as TextBox;
        if (pendingFilterEditor != null) pendingFilterEditor.Unloaded += FilterEditorClosed;
        pendingFilterProperty = (e.Column as DataGridBoundColumn)?.Binding is Binding binding ? binding.Path.Path : null;
        pendingFilterValue = pendingFilterEditor != null && ReadNumber(pendingFilterEditor.Text, out var value) ? value : 0;
        UpdateSaveButton();
    }
    void FilterEditorClosed(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox editor) editor.Unloaded -= FilterEditorClosed;
        if (!ReferenceEquals(sender, pendingFilterEditor)) return;
        pendingFilterEditor = null; pendingFilterProperty = null; UpdateSaveButton();
    }
    void PendingFilterTextChanged(object sender, TextChangedEventArgs e)
    { if (!updating && ReferenceEquals(e.OriginalSource, pendingFilterEditor)) UpdateSaveButton(); }
}
