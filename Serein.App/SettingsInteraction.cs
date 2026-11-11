using System.Windows;
using System.Windows.Controls;

namespace Serein.App;

public partial class MainWindow
{
    FrameworkElement GeneralSettings(Window window)
    {
        var panel = new StackPanel { Margin = new(8, 16, 8, 8) };

        ComboBox AddChoice(string label, string value, params (string Id, string Label)[] choices)
        {
            panel.Children.Add(new TextBlock { Text = label, Margin = new(0, 0, 0, 5) });
            var box = new ComboBox
            {
                DisplayMemberPath = "Item2",
                SelectedValuePath = "Item1",
                Margin = new(0, 0, 0, 16)
            };
            // Use properties because WPF bindings do not resolve ValueTuple fields.
            box.ItemsSource = choices.Select(choice => Tuple.Create(choice.Id, choice.Label)).ToArray();
            box.SelectedValue = value;
            if (box.SelectedIndex < 0) box.SelectedIndex = 0;
            System.Windows.Automation.AutomationProperties.SetName(box, label);
            panel.Children.Add(box);
            return box;
        }

        var language = AddChoice(T("Sprache"), state.Language, ("System", T("Windows-Einstellung")),
            ("de", "Deutsch"), ("en", "English"));
        var theme = AddChoice(T("Darstellung"), state.Appearance, ("Dark", T("Dunkel")), ("Light", T("Hell")));
        var accent = AddChoice(T("Akzentfarbe"), state.AccentColor, ("System", T("Windows-Einstellung")),
            ("Blue", T("Blau")), ("Green", T("Grün")), ("Orange", "Orange"), ("Purple", T("Violett")));
        void UpdateAppearance(object sender, SelectionChangedEventArgs e)
        {
            if (theme.SelectedValue is string mode && accent.SelectedValue is string color)
                ChangeAppearance(mode, color);
        }
        theme.SelectionChanged += UpdateAppearance;
        accent.SelectionChanged += UpdateAppearance;

        var restart = new System.Windows.Controls.Button
        {
            Content = T("Sprache übernehmen und neu starten"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            IsEnabled = false
        };
        language.SelectionChanged += (_, _) => restart.IsEnabled = (string?)language.SelectedValue != state.Language;
        restart.Click += (_, _) =>
        {
            if (language.SelectedValue is not string selected) return;
            var previous = state.Language;
            state.Language = selected;
            if (!SaveState())
            {
                state.Language = previous;
                return;
            }
            window.Close();
            if (smoke) return;
            ((App)System.Windows.Application.Current).RestartRequested = true;
            closing = true;
            Close();
        };
        panel.Children.Add(restart);
        return panel;
    }
}
