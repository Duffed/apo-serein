using System.Windows;
using System.Windows.Media;

namespace Serein.App;

public static class Appearance
{
    public static bool IsLight { get; private set; }

    public static void Apply(string mode, string accent = "System")
    {
        IsLight = mode == "Light";
        var resources = System.Windows.Application.Current.Resources;
        // Microsoft's Fluent controls, with semantic colors for the custom EQ surface.
        var source = new Uri($"pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.{(IsLight ? "Light" : "Dark")}.xaml");
        if (resources.MergedDictionaries[0].Source != source)
            resources.MergedDictionaries[0] = new ResourceDictionary { Source = source };
        void Set(string key, string light, string dark) => resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(IsLight ? light : dark));
        Set("Bg", "#F3F3F3", "#202020"); Set("Sidebar", "#F3F3F3", "#202020");
        Set("Panel", "#FFFFFF", "#2B2B2B"); Set("Text", "#1B1B1B", "#FFFFFF");
        Set("Muted", "#5D5D5D", "#C5C5C5"); Set("Line", "#D6D6D6", "#414141");
        Set("Selected", "#E4E4E4", "#383838"); Set("Hover", "#EAEAEA", "#333333");
        Set("Thumb", "#777777", "#999999"); Set("Danger", "#B32636", "#FF99A4");
        Set("EqActiveFill", "#267348", "#267348"); Set("EqActiveHover", "#205F3B", "#205F3B");
        Set("StatusSuccess", "#267348", "#77DBA4");
        resources["Accent"] = new SolidColorBrush(IsLight ? SystemColors.AccentColorDark1 : SystemColors.AccentColorLight2);
        switch (accent)
        {
            case "Blue": Set("Accent", "#005FB8", "#83BBFF"); break;
            case "Green": Set("Accent", "#176841", "#7AD7A3"); break;
            case "Orange": Set("Accent", "#9D381C", "#FFA68D"); break;
            case "Purple": Set("Accent", "#6843A5", "#C9ACFF"); break;
        }
        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            DarkChrome.Update(window);
            InvalidateCurves(window);
        }
    }

    static void InvalidateCurves(DependencyObject parent)
    {
        if (parent is CurveView curve) curve.InvalidateVisual();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) InvalidateCurves(VisualTreeHelper.GetChild(parent, i));
    }

    public static Brush Brush(string key) => (Brush)System.Windows.Application.Current.FindResource(key);
}
