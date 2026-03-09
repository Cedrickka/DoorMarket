namespace DoorMarket.Mobile.Components;

public partial class DmInput : ContentView
{
    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label),
        typeof(string),
        typeof(DmInput),
        string.Empty,
        propertyChanged: OnLabelChanged);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(DmInput),
        default(string),
        BindingMode.TwoWay);

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder),
        typeof(string),
        typeof(DmInput),
        string.Empty);

    public static readonly BindableProperty KeyboardProperty = BindableProperty.Create(
        nameof(Keyboard),
        typeof(Keyboard),
        typeof(DmInput),
        Keyboard.Default);

    public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create(
        nameof(IsPassword),
        typeof(bool),
        typeof(DmInput),
        false);

    public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(
        nameof(ReturnType),
        typeof(ReturnType),
        typeof(DmInput),
        ReturnType.Default);

    public static readonly BindableProperty LeftIconGlyphProperty = BindableProperty.Create(
        nameof(LeftIconGlyph),
        typeof(string),
        typeof(DmInput),
        default(string),
        propertyChanged: OnLeftIconGlyphChanged);

    public event EventHandler? Completed;
    public event EventHandler<TextChangedEventArgs>? TextChanged;

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool HasLabel => !string.IsNullOrWhiteSpace(Label);

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

    public Keyboard Keyboard
    {
        get => (Keyboard)GetValue(KeyboardProperty);
        set => SetValue(KeyboardProperty, value);
    }

    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    public ReturnType ReturnType
    {
        get => (ReturnType)GetValue(ReturnTypeProperty);
        set => SetValue(ReturnTypeProperty, value);
    }

    public string? LeftIconGlyph
    {
        get => (string?)GetValue(LeftIconGlyphProperty);
        set => SetValue(LeftIconGlyphProperty, value);
    }

    public bool HasLeftIcon => !string.IsNullOrWhiteSpace(LeftIconGlyph);

    public DmInput()
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

    private static void OnLabelChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DmInput input)
        {
            input.OnPropertyChanged(nameof(HasLabel));
        }
    }

    private static void OnLeftIconGlyphChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DmInput input)
        {
            input.OnPropertyChanged(nameof(HasLeftIcon));
        }
    }
}
