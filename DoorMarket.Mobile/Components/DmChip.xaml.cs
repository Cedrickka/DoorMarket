namespace DoorMarket.Mobile.Components;

public partial class DmChip : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(DmChip),
        string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public DmChip()
    {
        InitializeComponent();
    }
}
