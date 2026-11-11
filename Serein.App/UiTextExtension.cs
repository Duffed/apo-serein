global using static Serein.Core.UiLanguage;

using System.Windows.Markup;

namespace Serein.App;

[MarkupExtensionReturnType(typeof(string))]
public sealed class UiTextExtension(string text) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => T(text);
}
