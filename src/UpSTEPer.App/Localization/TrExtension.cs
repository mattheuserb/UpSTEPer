using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace UpSTEPer.App.Localization;

/// <summary>
/// <c>{loc:Tr Key}</c>: the text <c>Key</c> from <c>Strings.resx</c> in the current language,
/// updated when <see cref="Localizer.SetCulture"/> switches it.
/// </summary>
/// <param name="key">The resource name.</param>
internal sealed class TrExtension(string key) : MarkupExtension
{
    /// <summary>Gets the resource name.</summary>
    public string Key { get; } = key;

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay };
}
