using DoorMarket.Mobile.Models;

namespace DoorMarket.Mobile.Markup;

[ContentProperty(nameof(Glyph))]
public class MaterialIconExtension : IMarkupExtension<FontImageSource>
{
    public string Glyph { get; set; } = MaterialIcons.Info;

    public double Size { get; set; } = 18;

    public Color? Color { get; set; }

    public FontImageSource ProvideValue(IServiceProvider serviceProvider)
    {
        return new FontImageSource
        {
            FontFamily = "MaterialIcons",
            Glyph = Glyph,
            Size = Size,
            Color = Color ?? (Color)Microsoft.Maui.Controls.Application.Current!.Resources["DmTextPrimary"]
        };
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
