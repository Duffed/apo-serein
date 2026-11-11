using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Serein.Core;

namespace Serein.App;
public sealed class CurveView : FrameworkElement
{
    Profile? profile;
    CurveLayer layer;
    Observable? selected;
    Observable? hovered;
    readonly List<Handle> handles = [];
    DragSnapshot? drag;
    DragSnapshot? wheel;
    Point wheelPosition;
    bool axisLocked;
    readonly DispatcherTimer wheelTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    Vector grabOffset;
    sealed record Handle(Observable Model, string Label, Point Position, bool Enabled, bool Gain, bool Frequency);
    sealed record DragSnapshot(Profile Profile, Handle Handle, double Frequency, double Gain, double Q, bool UseQ, string? Type);
    public CurveView() { wheelTimer.Tick += (_, _) => EndWheel(false); Unloaded += (_, _) => CancelEdit(); }

    public Profile? Profile
    {
        get => profile;
        set { if (ReferenceEquals(profile, value)) return; CancelEdit(); profile = value; selected = hovered = null; handles.Clear(); InvalidateVisual(); }
    }
    public CurveLayer Layer { get => layer; set { if (layer == value) return; CancelEdit(); layer = value; selected = hovered = null; InvalidateVisual(); } }
    public bool Editable { get; set; }
    public bool Bypass { get; set; }
    public EqFilter? SelectedFilter => selected as EqFilter;
    public bool IsInteracting => drag != null || wheel != null;
    public event EventHandler<CancelEventArgs>? EditStarting;
    public event EventHandler? Edited;
    public event EventHandler? EditCompleted;

    double PlotWidth => Math.Max(1, ActualWidth - 55);
    double PlotHeight => Math.Max(1, ActualHeight - 42);
    double X(double hz) => 42 + Math.Log10(hz / 20) / 3 * PlotWidth;
    double Y(double db) => 12 + (18 - Math.Clamp(db, -24, 18)) / 42 * PlotHeight;
    double FrequencyAt(double x) => 20 * Math.Pow(1000, Math.Clamp((x - 42) / PlotWidth, 0, 1));
    double DbAt(double y) => Math.Clamp(18 - (y - 12) / PlotHeight * 42, -24, 18);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); handles.Clear();
        // Transparent hit area keeps mouse input available around small handles.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var muted = Appearance.Brush("Muted"); var grid = new Pen(Appearance.Brush("Line"), 1); var accent = Appearance.Brush("Accent");
        FormattedText Text(string s, Brush brush) => new(s, Culture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        void Label(string s, double x, double y) => dc.DrawText(Text(s, muted), new(x, y));
        foreach (double db in new[] { -24d, -18, -12, -6, 0, 6, 12, 18 }) { dc.DrawLine(grid, new(42, Y(db)), new(42 + PlotWidth, Y(db))); Label(db.ToString("+0;-0;0"), 3, Y(db) - 7); }
        foreach (double hz in new[] { 20d, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 }) { dc.DrawLine(grid, new(X(hz), 12), new(X(hz), 12 + PlotHeight)); Label(hz >= 1000 ? hz / 1000 + "k" : hz.ToString(), X(hz) - 8, 12 + PlotHeight + 10); }
        dc.DrawLine(new(muted, 1), new(42, Y(0)), new(42 + PlotWidth, Y(0)));
        if (Profile == null) return;
        double preamp = Response.EffectivePreamp(Profile);
        var response = Response.Compile(Profile);
        var geometry = new StreamGeometry(); var fill = new StreamGeometry();
        using (var path = geometry.Open()) using (var area = fill.Open())
        {
            area.BeginFigure(new(42, Y(0)), true, true);
            for (int i = 0; i <= 600; i++)
            {
                double hz = 20 * Math.Pow(1000, i / 600.0); double db = Bypass ? 0 : response(hz) + preamp;
                var pt = new Point(X(hz), Y(db)); if (i == 0) path.BeginFigure(pt, false, false); else path.LineTo(pt, true, false); area.LineTo(pt, true, false);
            }
            area.LineTo(new(42 + PlotWidth, Y(0)), true, false);
        }
        geometry.Freeze(); fill.Freeze(); var areaBrush = accent.Clone(); areaBrush.Opacity = .10; dc.DrawGeometry(areaBrush, null, fill);
        if (Editable && !Bypass && selected != null)
        {
            Func<double, double>? individual = null;
            if (selected is EqFilter filter && Profile.Filters.Contains(filter)) individual = Response.Compile(new Profile { Filters = [filter] });
            else if (selected is EqPoint point)
            {
                var points = Layer == CurveLayer.Adjustment ? Profile.Adjustment : Profile.Graphic;
                if (points.Contains(point)) individual = Response.Compile(new Profile { Graphic = new(points.Select(p => new EqPoint { Frequency = p.Frequency, Gain = ReferenceEquals(p, point) ? p.Gain : 0 })) });
            }
            if (individual != null)
            {
                var single = new StreamGeometry();
                using (var path = single.Open())
                    for (int i = 0; i <= 600; i++)
                    {
                        double hz = 20 * Math.Pow(1000, i / 600.0); var pt = new Point(X(hz), Y(individual(hz)));
                        if (i == 0) path.BeginFigure(pt, false, false); else path.LineTo(pt, true, false);
                    }
                single.Freeze(); dc.DrawGeometry(null, new Pen(muted, 1.5) { DashStyle = DashStyles.Dash }, single);
                var legend = Text(selected is EqFilter selectedFilter ? F("Filter {0} ({1}) · gestrichelt · ohne Preamp", Profile.Filters.IndexOf(selectedFilter) + 1, selectedFilter.Type) : T("Einzelband · gestrichelt · ohne Preamp"), muted);
                dc.DrawRectangle(Appearance.Brush("Panel"), null, new Rect(47, 15, legend.Width + 8, legend.Height + 4)); dc.DrawText(legend, new Point(51, 17));
            }
        }
        dc.DrawGeometry(null, new(accent, 2.3), geometry);
        if (!Editable) return;
        void Add(Observable model, double hz, string label, bool enabled, bool gain, bool frequency)
        {
            if (hz < 20 || hz > 20000) return;
            handles.Add(new(model, label, new(X(hz), Y(Bypass ? 0 : response(hz) + preamp)), enabled && !Bypass, gain, frequency));
        }
        if (Layer == CurveLayer.Filters)
            for (int i = 0; i < Profile.Filters.Count; i++) { var f = Profile.Filters[i]; Add(f, f.Frequency, $"Filter {i + 1} · {f.Type}", f.Enabled, CurveEditing.HasGain(f), true); }
        else
        {
            var points = Layer == CurveLayer.Adjustment ? Profile.Adjustment : Profile.Graphic;
            for (int i = 0; i < points.Count; i++) Add(points[i], points[i].Frequency, Layer == CurveLayer.Adjustment ? T("Grafischer EQ") : F("Importierter EQ · Punkt {0}", i + 1), true, true, Layer == CurveLayer.ImportedGraphic);
        }
        foreach (var handle in handles.OrderBy(h => ReferenceEquals(h.Model, selected)))
        {
            var brush = handle.Enabled ? accent : muted;
            double radius = ReferenceEquals(handle.Model, hovered) ? 5.5 : 4.5;
            dc.DrawEllipse(ReferenceEquals(handle.Model, selected) ? brush : Appearance.Brush("Panel"), new Pen(brush, 1.5), handle.Position, radius, radius);
            if (ReferenceEquals(handle.Model, hovered ?? selected))
            {
                var value = Text(Describe(handle, false), Appearance.Brush("Text"));
                double x = Math.Clamp(handle.Position.X + 9, 42, Math.Max(42, 42 + PlotWidth - value.Width - 8));
                double y = Math.Clamp(handle.Position.Y - 25, 12, Math.Max(12, 12 + PlotHeight - value.Height - 5));
                dc.DrawRoundedRectangle(Appearance.Brush("Panel"), null, new Rect(x - 3, y - 2, value.Width + 6, value.Height + 4), 2, 2);
                dc.DrawText(value, new Point(x, y));
            }
        }
    }

    Handle? Hit(Point point) => handles.OrderBy(h => (h.Position - point).LengthSquared).FirstOrDefault(h => (h.Position - point).LengthSquared <= 100);
    static string Describe(Handle handle, bool instruction)
    {
        double hz = handle.Model is EqFilter f ? f.Frequency : ((EqPoint)handle.Model).Frequency;
        double gain = handle.Model is EqFilter filter ? filter.Gain : ((EqPoint)handle.Model).Gain;
        string value = F("{0} · {1:0.#} Hz", handle.Label, hz) + (handle.Gain ? F(" · {0:+0.##;-0.##;0} dB", gain) : "");
        if (handle.Model is EqFilter qFilter) value += qFilter.UseQ ? F(" · Q {0:0.###}", qFilter.Q) : T(" · Q Standard");
        if (!handle.Enabled) value += T(" · deaktiviert");
        return !instruction ? value : value + (handle.Enabled ? T("\nZiehen: ") + (handle.Frequency ? T("Frequenz") : "") + (handle.Frequency && handle.Gain ? T(" und ") : "") + (handle.Gain ? "Gain" : "") + (handle.Gain ? T(" · Shift: nur vertikal") : "") + (handle.Model is EqFilter ? T("\nMausrad: Q ändern") : "") + T(" · Escape: zurücksetzen") : T("\nEQ oder Filter ist deaktiviert."));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (drag != null)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndEdit(false); return; }
            var point = e.GetPosition(this) + grabOffset;
            axisLocked = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            if (drag.Handle.Model is EqFilter filter) CurveEditing.MoveFilter(drag.Profile, filter, FrequencyAt(point.X), DbAt(point.Y), !axisLocked);
            else CurveEditing.MoveGraphic(drag.Profile, (EqPoint)drag.Handle.Model, drag.Handle.Frequency && !axisLocked, FrequencyAt(point.X), DbAt(point.Y));
            UpdateModifierCursor(Keyboard.Modifiers);
            InvalidateVisual(); Edited?.Invoke(this, EventArgs.Empty); e.Handled = true; return;
        }
        if (wheel != null && (e.GetPosition(this) - wheelPosition).Length > 14) EndWheel(false);
        var hit = wheel?.Handle ?? (Editable ? Hit(e.GetPosition(this)) : null);
        if (!ReferenceEquals(hovered, hit?.Model)) { hovered = hit?.Model; InvalidateVisual(); }
        UpdateModifierCursor(Keyboard.Modifiers);
        ToolTip = hit == null ? null : Describe(hit, true);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var hit = Editable ? Hit(e.GetPosition(this)) : null;
        if (hit is not { Enabled: true } || Profile == null) return;
        EndWheel(false);
        selected = hit.Model;
        var starting = new CancelEventArgs(); EditStarting?.Invoke(this, starting);
        if (starting.Cancel) return;
        double hz = hit.Model is EqFilter f ? f.Frequency : ((EqPoint)hit.Model).Frequency;
        double gain = hit.Model is EqFilter filter ? filter.Gain : ((EqPoint)hit.Model).Gain;
        var q = hit.Model as EqFilter;
        drag = new(Profile, hit, hz, gain, q?.Q ?? 1, q?.UseQ ?? false, q?.Type); grabOffset = hit.Position - e.GetPosition(this); axisLocked = false;
        Focus(); if (!CaptureMouse()) { drag = null; EditCompleted?.Invoke(this, EventArgs.Empty); return; }
        UpdateModifierCursor(Keyboard.Modifiers);
        InvalidateVisual(); e.Handled = true;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { base.OnMouseLeftButtonUp(e); if (drag != null) { EndEdit(false); e.Handled = true; } }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); EndEdit(false); }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.Key == Key.Escape && IsInteracting) { CancelEdit(); e.Handled = true; } else if (e.Key is Key.LeftShift or Key.RightShift) UpdateModifierCursor(Keyboard.Modifiers); }
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key is Key.LeftShift or Key.RightShift && drag != null && axisLocked)
        {
            double hz = drag.Handle.Model is EqFilter f ? f.Frequency : ((EqPoint)drag.Handle.Model).Frequency;
            grabOffset.X = X(hz) - Mouse.GetPosition(this).X; axisLocked = false;
        }
        if (e.Key is Key.LeftShift or Key.RightShift) UpdateModifierCursor(Keyboard.Modifiers);
    }
    protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); if (drag == null) { EndWheel(false); Cursor = Cursors.Arrow; ToolTip = null; hovered = null; InvalidateVisual(); } }
    public void UpdateModifierCursor(ModifierKeys modifiers)
    {
        var hit = drag?.Handle ?? wheel?.Handle ?? (Editable && IsMouseOver ? Hit(Mouse.GetPosition(this)) : null);
        Cursor = hit is not { Enabled: true } ? Cursors.Arrow : modifiers.HasFlag(ModifierKeys.Shift) ? hit.Gain ? Cursors.SizeNS : Cursors.Arrow : hit.Frequency ? hit.Gain ? Cursors.SizeAll : Cursors.SizeWE : Cursors.SizeNS;
    }
    public void SelectFilter(EqFilter? filter)
    {
        if (Layer != CurveLayer.Filters || filter != null && Profile?.Filters.Contains(filter) != true) return;
        selected = filter; InvalidateVisual();
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var hit = drag?.Handle ?? wheel?.Handle ?? (Editable ? Hit(e.GetPosition(this)) : null);
        if (hit is not { Enabled: true, Model: EqFilter filter } || Profile == null || Bypass) return;
        if (drag == null && wheel == null)
        {
            selected = filter; var starting = new CancelEventArgs(); EditStarting?.Invoke(this, starting);
            if (starting.Cancel) return;
            wheel = new(Profile, hit, filter.Frequency, filter.Gain, filter.Q, filter.UseQ, filter.Type);
            wheelPosition = e.GetPosition(this); Focus();
        }
        if (CurveEditing.ChangeQ(filter, e.Delta)) { InvalidateVisual(); Edited?.Invoke(this, EventArgs.Empty); ToolTip = Describe(hit, true); }
        if (drag == null) { wheelTimer.Stop(); wheelTimer.Start(); }
        e.Handled = true;
    }
    public void CancelEdit() { EndWheel(true); EndEdit(true); }
    static void Restore(DragSnapshot snapshot)
    {
        if (snapshot.Handle.Model is EqFilter f) { f.Type = snapshot.Type!; f.Frequency = snapshot.Frequency; f.Gain = snapshot.Gain; f.Q = snapshot.Q; f.UseQ = snapshot.UseQ; }
        else if (snapshot.Handle.Model is EqPoint p) { p.Frequency = snapshot.Frequency; p.Gain = snapshot.Gain; }
    }
    void EndWheel(bool cancel)
    {
        wheelTimer.Stop(); var snapshot = wheel; if (snapshot == null) return; wheel = null;
        if (cancel) { Restore(snapshot); Edited?.Invoke(this, EventArgs.Empty); }
        InvalidateVisual(); EditCompleted?.Invoke(this, EventArgs.Empty);
    }
    void EndEdit(bool cancel)
    {
        var snapshot = drag; if (snapshot == null) return; drag = null;
        if (cancel)
        {
            Restore(snapshot);
            Edited?.Invoke(this, EventArgs.Empty);
        }
        if (IsMouseCaptured) ReleaseMouseCapture();
        InvalidateVisual(); EditCompleted?.Invoke(this, EventArgs.Empty);
    }
}
