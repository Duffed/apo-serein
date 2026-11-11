namespace Serein.App;

public sealed record FilterTypeChoice(string Code, string Label, string Description);

public static class FilterTypes
{
    public static readonly FilterTypeChoice[] Choices =
    [
        new("PK", "PK · Glockenfilter", "Hebt einen Bereich um die Frequenz an oder senkt ihn ab. Gain bestimmt die Stärke, Q die Breite."),
        new("LS", "LS · Bass-Shelf", "Hebt tiefe Frequenzen flächig an oder senkt sie ab. Bei explizitem Q bezeichnet die Frequenz die Eckfrequenz."),
        new("HS", "HS · Höhen-Shelf", "Hebt hohe Frequenzen flächig an oder senkt sie ab. Bei explizitem Q bezeichnet die Frequenz die Eckfrequenz."),
        new("LSC", "LSC · Bass-Shelf (Mitte)", "Bass-Shelf mit Frequenzangabe in der Mitte des Übergangs."),
        new("HSC", "HSC · Höhen-Shelf (Mitte)", "Höhen-Shelf mit Frequenzangabe in der Mitte des Übergangs."),
        new("LP", "LP · Tiefpass", "Lässt tiefe Frequenzen passieren und schwächt hohe ab. Gain wird nicht verwendet."),
        new("HP", "HP · Hochpass", "Lässt hohe Frequenzen passieren und schwächt tiefe ab. Gain wird nicht verwendet."),
        new("LPQ", "LPQ · Tiefpass mit Q", "Tiefpass mit einstellbarer Güte für den Übergang. Gain wird nicht verwendet."),
        new("HPQ", "HPQ · Hochpass mit Q", "Hochpass mit einstellbarer Güte für den Übergang. Gain wird nicht verwendet."),
        new("NO", "NO · Kerbfilter", "Senkt einen engen Bereich um die Frequenz ab, etwa eine einzelne Störfrequenz. Gain wird nicht verwendet."),
        new("AP", "AP · Allpass", "Verändert die Phase, ohne den Pegel-Frequenzgang zu verändern. Gain wird nicht verwendet."),
        new("BP", "BP · Bandpass", "Lässt einen Frequenzbereich passieren und schwächt tiefere und höhere Frequenzen ab. Gain wird nicht verwendet.")
    ];
}

public sealed class FilterTypeDescriptionConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => FilterTypes.Choices.FirstOrDefault(choice => choice.Code == value as string)?.Description ?? "Filterform";
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
