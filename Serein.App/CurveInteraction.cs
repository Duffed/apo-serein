using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Serein.Core;

namespace Serein.App;

public partial class MainWindow
{
    Guid? curveProfileId;
    bool syncingEqView;
    void UpdateCurveLayers()
    {
        var previous = CurveLayerTabs.SelectedItem as CurveLayerChoice;
        var layers = new List<CurveLayerChoice> { new(CurveLayer.Adjustment, T("Grafischer EQ")), new(CurveLayer.Filters, T("Parametrische Filter")) };
        if (current.Graphic.Count > 0) layers.Add(new(CurveLayer.ImportedGraphic, T("Importierter EQ")));
        bool wasUpdating = updating; updating = true;
        try
        {
            CurveLayerTabs.ItemsSource = layers;
            var initial = current.Filters.Count > 0 ? CurveLayer.Filters : current.Graphic.Count > 0 ? CurveLayer.ImportedGraphic : CurveLayer.Adjustment;
            var choice = curveProfileId == current.Id ? layers.FirstOrDefault(c => c.Layer == previous?.Layer) ?? layers[0] : layers.First(c => c.Layer == initial);
            curveProfileId = current.Id; CurveLayerTabs.SelectedItem = choice; ShowCurveLayer(choice.Layer);
        }
        finally { updating = wasUpdating; }
    }

    void ChangeCurveLayer(object sender, SelectionChangedEventArgs e)
    {
        if (!updating && !syncingEqView && e.Source == CurveLayerTabs && CurveLayerTabs.SelectedItem is CurveLayerChoice choice) ShowCurveLayer(choice.Layer);
    }

    void ShowCurveLayer(CurveLayer layer)
    {
        syncingEqView = true;
        try
        {
            Curve.Layer = layer; EqTabs.SelectedIndex = layer == CurveLayer.Filters ? 1 : 0;
            BandList.ItemsSource = layer == CurveLayer.ImportedGraphic ? current.Graphic : current.Adjustment;
            BandActions.Visibility = layer == CurveLayer.ImportedGraphic ? Visibility.Collapsed : Visibility.Visible;
            GraphicStageLabel.Text = layer == CurveLayer.ImportedGraphic ? T("Importierter grafischer EQ") : T("Persönliche Anpassung · zusätzlich zur importierten Korrektur");
            if (layer == CurveLayer.Filters && FilterGrid.SelectedItem is EqFilter filter) Curve.SelectFilter(filter);
        }
        finally { syncingEqView = false; }
    }

    void ChangeEqView(object sender, SelectionChangedEventArgs e)
    {
        if (updating || syncingEqView || e.Source != EqTabs || current == null) return;
        var layer = EqTabs.SelectedIndex == 1 ? CurveLayer.Filters : CurveLayer.Adjustment;
        CurveLayerTabs.SelectedItem = CurveLayerTabs.Items.Cast<CurveLayerChoice>().FirstOrDefault(c => c.Layer == layer);
    }

    void StartCurveEdit(object? sender, CancelEventArgs e)
    {
        if (!FilterGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !FilterGrid.CommitEdit(DataGridEditingUnit.Row, true))
        { e.Cancel = true; Status.Text = T("Bitte zuerst die ungültige Filtereingabe korrigieren."); return; }
        try { ApoFormat.Validate(current); }
        catch (Exception ex) { e.Cancel = true; Status.Text = ex.Message; return; }
        editTimer.Stop();
        if (Curve.SelectedFilter is EqFilter filter) { FilterGrid.SelectedItem = filter; FilterGrid.ScrollIntoView(filter); }
        HistoryFor(current).Begin(); UpdateHistoryButtons();
    }

    void CurveEdited(object? sender, EventArgs e) => ScheduleChange();
    void SelectCurveFilter(object sender, SelectionChangedEventArgs e) { if (!updating) Curve.SelectFilter(FilterGrid.SelectedItem as EqFilter); }
    async void CompleteCurveEdit(object? sender, EventArgs e) { editTimer.Stop(); HistoryFor(current).End(); UpdateHistoryButtons(); await CommitChanges(); }
}
