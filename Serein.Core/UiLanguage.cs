using System.Globalization;

namespace Serein.Core;

public static partial class UiLanguage
{
    public static string WindowsLanguage { get; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("de-DE");

    public static void Apply(string? language)
    {
        var code = language is "de" or "en" ? language : WindowsLanguage;
        Culture = CultureInfo.GetCultureInfo(code == "de" ? "de-DE" : "en-US");
        CultureInfo.CurrentUICulture = Culture;
    }

    public static string T(string text) =>
        Culture.Name == "en-US" && English.TryGetValue(text, out var translation) ? translation : text;

    public static string F(string text, params object?[] values) => string.Format(Culture, T(text), values);
}
