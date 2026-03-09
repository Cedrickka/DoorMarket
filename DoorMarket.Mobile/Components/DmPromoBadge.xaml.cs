namespace DoorMarket.Mobile.Components;

public partial class DmPromoBadge : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(DmPromoBadge),
        "-20%");

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public DmPromoBadge()
    {
        InitializeComponent();
    }
}
