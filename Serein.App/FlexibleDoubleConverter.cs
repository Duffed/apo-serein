using System.Globalization;
using System.Windows.Data;

namespace Serein.App;
public sealed class FlexibleDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is double n ? n.ToString("0.########", CultureInfo.GetCultureInfo("de-DE")) : value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (double.TryParse(value?.ToString()?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)) return n;
        throw new FormatException("Bitte eine Zahl mit Komma oder Punkt eingeben.");
    }
}
