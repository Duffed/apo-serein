using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Serein.Core;

namespace Serein.App;

public partial class MainWindow
{
    const string ProfileOrderFormat = "Serein.ProfileOrder";
    Profile? pendingDrag, draggedProfile, dropAnchor;
    Point dragStart, dropPosition;
    bool dropAfter;
    FrameworkElement? dragGrip;
    ProfileInsertionAdorner? insertion;
    AdornerLayer? insertionLayer;
    DispatcherTimer? dragScroll;

    void ProfileDragStart(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Profile profile } grip) return;
        // A drag handle must not select (and apply) an inactive audio profile.
        pendingDrag = profile; dragGrip = grip; dragStart = e.GetPosition(ProfileList);
        grip.CaptureMouse(); e.Handled = true;
    }

    void ProfileDragRelease(object sender, MouseButtonEventArgs e)
    {
        if (pendingDrag == null) return;
        pendingDrag = null; dragGrip?.ReleaseMouseCapture(); dragGrip = null; e.Handled = true;
    }

    void ProfileDragMove(object sender, MouseEventArgs e)
    {
        if (pendingDrag == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { pendingDrag = null; dragGrip?.ReleaseMouseCapture(); dragGrip = null; return; }
        var delta = e.GetPosition(ProfileList) - dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        draggedProfile = pendingDrag; pendingDrag = null;
        dragGrip?.ReleaseMouseCapture(); dragGrip = null;
        var container = ProfileList.ItemContainerGenerator.ContainerFromItem(draggedProfile) as ListBoxItem;
        if (container != null) container.Opacity = .45;
        try
        {
            dragScroll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            dragScroll.Tick += ScrollWhileDragging;
            DragDrop.DoDragDrop(ProfileList, new DataObject(ProfileOrderFormat, draggedProfile.Id.ToString()), DragDropEffects.Move);
        }
        finally
        {
            if (container != null) container.Opacity = 1;
            draggedProfile = null; ClearDropIndicator();
            if (dragScroll != null) { dragScroll.Stop(); dragScroll.Tick -= ScrollWhileDragging; dragScroll = null; }
        }
    }

    bool IsProfileDrag(DragEventArgs e) => draggedProfile != null && profiles.Contains(draggedProfile)
        && e.Data.GetDataPresent(ProfileOrderFormat) && e.Data.GetData(ProfileOrderFormat) as string == draggedProfile.Id.ToString();

    void ProfileDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true; e.Effects = DragDropEffects.None;
        if (!IsProfileDrag(e)) return;
        e.Effects = DragDropEffects.Move; dropPosition = e.GetPosition(ProfileList);
        UpdateDropIndicator(); dragScroll?.Start();
    }

    void UpdateDropIndicator()
    {
        var hit = ProfileList.InputHitTest(dropPosition) as DependencyObject;
        var row = hit == null ? null : ItemsControl.ContainerFromElement(ProfileList, hit) as ListBoxItem;
        if (row == null)
        {
            // Gaps between cards and unused space below search results belong to the nearest row.
            row = ProfileList.Items.Cast<Profile>()
                .Select(p => ProfileList.ItemContainerGenerator.ContainerFromItem(p) as ListBoxItem)
                .Where(item => item != null)
                .OrderBy(item => { var top = item!.TranslatePoint(new Point(), ProfileList).Y; return Math.Abs(dropPosition.Y - Math.Clamp(dropPosition.Y, top, top + item.ActualHeight)); })
                .FirstOrDefault();
        }
        if (row?.DataContext is not Profile anchor) { ClearDropIndicator(); return; }
        var top = row.TranslatePoint(new Point(), ProfileList).Y;
        dropAnchor = anchor; dropAfter = dropPosition.Y >= top + row.ActualHeight / 2;
        double y = top + (dropAfter ? row.ActualHeight + 2 : -2);
        insertionLayer ??= AdornerLayer.GetAdornerLayer(ProfileList);
        if (insertionLayer == null) return;
        if (insertion == null) { insertion = new ProfileInsertionAdorner(ProfileList); insertionLayer.Add(insertion); }
        insertion.Position = Math.Clamp(y, 2, Math.Max(2, ProfileList.ActualHeight - 2)); insertion.InvalidateVisual();
    }

    void ScrollWhileDragging(object? sender, EventArgs e)
    {
        var viewer = FindProfileViewer(ProfileList);
        if (viewer == null) return;
        double step = dropPosition.Y < 28 ? -14 : dropPosition.Y > ProfileList.ActualHeight - 28 ? 14 : 0;
        if (step == 0) return;
        viewer.ScrollToVerticalOffset(Math.Clamp(viewer.VerticalOffset + step, 0, viewer.ScrollableHeight));
        ProfileList.UpdateLayout(); UpdateDropIndicator();
    }

    void ProfileDragLeave(object sender, DragEventArgs e)
    {
        var point = e.GetPosition(ProfileList);
        if (point.X >= 0 && point.X <= ProfileList.ActualWidth && point.Y >= 0 && point.Y <= ProfileList.ActualHeight) return;
        ClearDropIndicator(); dragScroll?.Stop();
    }

    void ClearDropIndicator()
    {
        if (insertion != null) insertionLayer?.Remove(insertion);
        insertion = null; insertionLayer = null; dropAnchor = null;
    }

    void ProfileDrop(object sender, DragEventArgs e)
    {
        e.Handled = true; e.Effects = DragDropEffects.None;
        if (!IsProfileDrag(e)) return;
        dropPosition = e.GetPosition(ProfileList); UpdateDropIndicator();
        if (draggedProfile != null && dropAnchor != null && MoveProfile(draggedProfile, dropAnchor, dropAfter)) e.Effects = DragDropEffects.Move;
        ClearDropIndicator(); dragScroll?.Stop();
    }

    void ProfileOrderKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is not (Key.Up or Key.Down) || ProfileList.SelectedItem is not Profile profile) return;
        e.Handled = true;
        var visible = ProfileList.Items.Cast<Profile>().ToList();
        int index = visible.IndexOf(profile) + (key == Key.Up ? -1 : 1);
        if (index >= 0 && index < visible.Count && MoveProfile(profile, visible[index], key == Key.Down))
        { ProfileList.Focus(); ProfileList.ScrollIntoView(profile); }
    }

    bool MoveProfile(Profile profile, Profile anchor, bool after)
    {
        int from = profiles.IndexOf(profile), to = profiles.IndexOf(anchor);
        if (from < 0 || to < 0 || profile == anchor) return false;
        to += after ? 1 : 0; if (from < to) to--;
        if (from == to) return false;
        var viewer = FindProfileViewer(ProfileList); double offset = viewer?.VerticalOffset ?? 0;
        updating = true;
        try { profiles.Move(from, to); }
        finally { updating = false; }
        // Preserve the current selection/device assignment, including while search is active.
        RefreshProfiles(); ProfileList.UpdateLayout(); viewer?.ScrollToVerticalOffset(offset);
        SaveState(); BuildTray();
        return true;
    }

    static ScrollViewer? FindProfileViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindProfileViewer(VisualTreeHelper.GetChild(root, i)) is ScrollViewer found) return found;
        return null;
    }

    sealed class ProfileInsertionAdorner(UIElement element) : Adorner(element)
    {
        public double Position { get; set; }
        protected override void OnRender(DrawingContext dc)
        {
            IsHitTestVisible = false;
            var brush = Appearance.Brush("Accent");
            dc.DrawLine(new Pen(brush, 2), new Point(5, Position), new Point(Math.Max(5, AdornedElement.RenderSize.Width - 6), Position));
            dc.DrawEllipse(brush, null, new Point(5, Position), 3, 3);
        }
    }
}
