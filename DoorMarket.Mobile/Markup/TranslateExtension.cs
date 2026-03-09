using DoorMarket.Mobile.Localization;
using Microsoft.Maui.Controls.Xaml;

namespace DoorMarket.Mobile.Markup;

[ContentProperty(nameof(Key))]
public sealed class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding($"[{Key}]", source: LocalizationResourceManager.Instance);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
        => ProvideValue(serviceProvider);
}
