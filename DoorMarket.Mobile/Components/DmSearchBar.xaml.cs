namespace DoorMarket.Mobile.Components;

public partial class DmSearchBar : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(DmSearchBar),
        default(string),
        BindingMode.TwoWay);

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder),
        typeof(string),
        typeof(DmSearchBar),
        "Rechercher");

    public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(
        nameof(ReturnType),
        typeof(ReturnType),
        typeof(DmSearchBar),
        ReturnType.Search);

    public event EventHandler? Completed;
    public event EventHandler<TextChangedEventArgs>? TextChanged;

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public ReturnType ReturnType
    {
        get => (ReturnType)GetValue(ReturnTypeProperty);
        set => SetValue(ReturnTypeProperty, value);
    }

    public DmSearchBar()
    {
        InitializeComponent();
    }

    private void OnEntryCompleted(object sender, EventArgs e)
    {
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void OnEntryTextChanged(object sender, TextChangedEventArgs e)
    {
        TextChanged?.Invoke(this, e);
    }
}
