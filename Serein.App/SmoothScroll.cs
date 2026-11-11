using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Serein.App;

public static class SmoothScroll
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(SmoothScroll), new PropertyMetadata(false, Changed));
    static readonly DependencyProperty ControllerProperty = DependencyProperty.RegisterAttached("Controller", typeof(Controller), typeof(SmoothScroll));
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not ListBox list) return;
        if ((bool)e.NewValue) { list.Loaded += Loaded; if (list.IsLoaded) Loaded(list, new()); }
        else { list.Loaded -= Loaded; (list.GetValue(ControllerProperty) as Controller)?.Dispose(); }
    }
    static void Loaded(object sender, RoutedEventArgs e)
    {
        var list = (ListBox)sender;
        if (list.GetValue(ControllerProperty) == null && FindViewer(list) is ScrollViewer viewer) list.SetValue(ControllerProperty, new Controller(list, viewer));
    }
    static ScrollViewer? FindViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) if (FindViewer(VisualTreeHelper.GetChild(root, i)) is ScrollViewer child) return child;
        return null;
    }
    sealed class Controller : IDisposable
    {
        readonly ListBox list; readonly ScrollViewer viewer;
        bool running; double target; DateTime lastFrame;
        public Controller(ListBox list, ScrollViewer viewer)
        {
            this.list = list; this.viewer = viewer;
            list.PreviewMouseWheel += Wheel; list.PreviewMouseDown += MouseDown; list.PreviewKeyDown += KeyDown;
            list.SelectionChanged += SelectionChanged; list.SizeChanged += SizeChanged; list.Unloaded += Unloaded;
        }
        void Wheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || Keyboard.Modifiers != ModifierKeys.None || viewer.ScrollableHeight <= 0) return;
            if (!running) target = viewer.VerticalOffset;
            double step = SystemParameters.WheelScrollLines < 0 ? viewer.ViewportHeight * .9 : Math.Max(1, SystemParameters.WheelScrollLines) * 24;
            double next = Math.Clamp(target - e.Delta / 120.0 * step, 0, viewer.ScrollableHeight);
            if (Math.Abs(next - target) < .01 && !running) return;
            target = next; e.Handled = true;
            if (!running) { running = true; lastFrame = DateTime.UtcNow; CompositionTarget.Rendering += Render; }
        }
        void Render(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow; var dt = Math.Clamp((now-lastFrame).TotalSeconds, .001, .05); lastFrame = now;
            target = Math.Clamp(target, 0, viewer.ScrollableHeight);
            var distance = target - viewer.VerticalOffset;
            if (Math.Abs(distance) < .35) { viewer.ScrollToVerticalOffset(target); Stop(); return; }
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset + distance * (1-Math.Exp(-dt / .055)));
        }
        void Stop() { if (!running) return; running = false; CompositionTarget.Rendering -= Render; }
        void MouseDown(object sender, MouseButtonEventArgs e) => Stop();
        void KeyDown(object sender, KeyEventArgs e) => Stop();
        void SelectionChanged(object sender, SelectionChangedEventArgs e) => Stop();
        void SizeChanged(object sender, SizeChangedEventArgs e) => Stop();
        void Unloaded(object sender, RoutedEventArgs e) => Dispose();
        public void Dispose()
        {
            Stop(); list.PreviewMouseWheel -= Wheel; list.PreviewMouseDown -= MouseDown; list.PreviewKeyDown -= KeyDown;
            list.SelectionChanged -= SelectionChanged; list.SizeChanged -= SizeChanged; list.Unloaded -= Unloaded; list.ClearValue(ControllerProperty);
        }
    }
}
