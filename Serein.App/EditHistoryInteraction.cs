using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Serein.Core;

namespace Serein.App;

public partial class MainWindow
{
    readonly Dictionary<Guid, EqEditHistory> editHistories = [];
    EqEditHistory HistoryFor(Profile p)
    {
        if (!editHistories.TryGetValue(p.Id, out var history)) editHistories[p.Id] = history = new EqEditHistory(p);
        return history;
    }
    void UpdateHistoryButtons()
    {
        if (current == null) return;
        var history = HistoryFor(current); UndoEditButton.IsEnabled = history.CanUndo; RedoEditButton.IsEnabled = history.CanRedo;
        UndoEditButton.Visibility = history.CanUndo ? Visibility.Visible : Visibility.Collapsed;
        RedoEditButton.Visibility = history.CanRedo ? Visibility.Visible : Visibility.Collapsed;
        HistoryActions.Visibility = history.CanUndo || history.CanRedo ? Visibility.Visible : Visibility.Collapsed;
        UpdatePresetButton();
    }
    async void UndoEqEdit(object sender, RoutedEventArgs e) => await ReplayEdit(false);
    async void RedoEqEdit(object sender, RoutedEventArgs e) => await ReplayEdit(true);
    async Task ReplayEdit(bool redo)
    {
        if (current == null) return;
        if (Curve.IsInteracting) { Curve.CancelEdit(); return; }
        if (!FilterGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !FilterGrid.CommitEdit(DataGridEditingUnit.Row, true))
        { Status.Text = "Bitte zuerst die ungültige Filtereingabe korrigieren."; return; }
        editTimer.Stop();
        try
        {
            ApoFormat.Validate(current); var history = HistoryFor(current); bool restored;
            updating = true;
            try { restored = redo ? history.Redo() : history.Undo(); if (restored) AttachProfile(); }
            finally { updating = false; }
            if (restored) { Curve.Profile = null; Populate(); await CommitChanges(); }
            UpdateHistoryButtons();
        }
        catch (Exception ex) { Error(ex); }
    }
    async void EditHistoryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftShift or Key.RightShift && (Curve.IsMouseOver || Curve.IsInteracting)) Curve.UpdateModifierCursor(Keyboard.Modifiers | ModifierKeys.Shift);
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;
        bool redo = e.Key == Key.Y || e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (!redo && e.Key != Key.Z) return;
        // Text entry retains the standard editor undo history.
        for (DependencyObject? node = Keyboard.FocusedElement as DependencyObject; node != null;)
        {
            if (node is TextBoxBase) return;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        e.Handled = true; await ReplayEdit(redo);
    }
    void CurveModifierKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftShift or Key.RightShift && (Curve.IsMouseOver || Curve.IsInteracting)) Curve.UpdateModifierCursor(Keyboard.Modifiers);
    }
}
