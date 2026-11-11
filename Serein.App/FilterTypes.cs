namespace Serein.App;

public sealed record FilterTypeChoice(string Code, string Label, string Description);

public static class FilterTypes
{
    public static readonly FilterTypeChoice[] Choices =
    [
        new("PK", T("PK · Glockenfilter"), T("Hebt einen Bereich um die Frequenz an oder senkt ihn ab. Gain bestimmt die Stärke, Q die Breite.")),
        new("LS", T("LS · Bass-Shelf"), T("Hebt tiefe Frequenzen flächig an oder senkt sie ab. Bei explizitem Q bezeichnet die Frequenz die Eckfrequenz.")),
        new("HS", T("HS · Höhen-Shelf"), T("Hebt hohe Frequenzen flächig an oder senkt sie ab. Bei explizitem Q bezeichnet die Frequenz die Eckfrequenz.")),
        new("LSC", T("LSC · Bass-Shelf (Mitte)"), T("Bass-Shelf mit Frequenzangabe in der Mitte des Übergangs.")),
        new("HSC", T("HSC · Höhen-Shelf (Mitte)"), T("Höhen-Shelf mit Frequenzangabe in der Mitte des Übergangs.")),
        new("LP", T("LP · Tiefpass"), T("Lässt tiefe Frequenzen passieren und schwächt hohe ab. Gain wird nicht verwendet.")),
        new("HP", T("HP · Hochpass"), T("Lässt hohe Frequenzen passieren und schwächt tiefe ab. Gain wird nicht verwendet.")),
        new("LPQ", T("LPQ · Tiefpass mit Q"), T("Tiefpass mit einstellbarer Güte für den Übergang. Gain wird nicht verwendet.")),
        new("HPQ", T("HPQ · Hochpass mit Q"), T("Hochpass mit einstellbarer Güte für den Übergang. Gain wird nicht verwendet.")),
        new("NO", T("NO · Kerbfilter"), T("Senkt einen engen Bereich um die Frequenz ab, etwa eine einzelne Störfrequenz. Gain wird nicht verwendet.")),
        new("AP", T("AP · Allpass"), T("Verändert die Phase, ohne den Pegel-Frequenzgang zu verändern. Gain wird nicht verwendet.")),
        new("BP", T("BP · Bandpass"), T("Lässt einen Frequenzbereich passieren und schwächt tiefere und höhere Frequenzen ab. Gain wird nicht verwendet."))
    ];
}

public sealed class FilterTypeDescriptionConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => FilterTypes.Choices.FirstOrDefault(choice => choice.Code == value as string)?.Description ?? T("Filterform");
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
