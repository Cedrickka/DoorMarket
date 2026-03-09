using System.Windows.Input;

namespace DoorMarket.Mobile.Components;

public partial class DmBottomNav : ContentView
{
    public static readonly BindableProperty HomeCommandProperty = BindableProperty.Create(
        nameof(HomeCommand), typeof(ICommand), typeof(DmBottomNav));

    public static readonly BindableProperty CategoriesCommandProperty = BindableProperty.Create(
        nameof(CategoriesCommand), typeof(ICommand), typeof(DmBottomNav));

    public static readonly BindableProperty OrdersCommandProperty = BindableProperty.Create(
        nameof(OrdersCommand), typeof(ICommand), typeof(DmBottomNav));

    public static readonly BindableProperty ProfileCommandProperty = BindableProperty.Create(
        nameof(ProfileCommand), typeof(ICommand), typeof(DmBottomNav));

    public static readonly BindableProperty SelectedColorProperty = BindableProperty.Create(
        nameof(SelectedColor), typeof(Color), typeof(DmBottomNav), Color.FromArgb("#FF7A00"), propertyChanged: OnColorChanged);

    public static readonly BindableProperty DefaultColorProperty = BindableProperty.Create(
        nameof(DefaultColor), typeof(Color), typeof(DmBottomNav), Color.FromArgb("#B2002D5E"), propertyChanged: OnColorChanged);

    public static readonly BindableProperty ActiveTabProperty = BindableProperty.Create(
        nameof(ActiveTab), typeof(int), typeof(DmBottomNav), 0, propertyChanged: OnActiveTabChanged);

    public ICommand? HomeCommand
    {
        get => (ICommand?)GetValue(HomeCommandProperty);
        set => SetValue(HomeCommandProperty, value);
    }

    public ICommand? CategoriesCommand
    {
        get => (ICommand?)GetValue(CategoriesCommandProperty);
        set => SetValue(CategoriesCommandProperty, value);
    }

    public ICommand? OrdersCommand
    {
        get => (ICommand?)GetValue(OrdersCommandProperty);
        set => SetValue(OrdersCommandProperty, value);
    }

    public ICommand? ProfileCommand
    {
        get => (ICommand?)GetValue(ProfileCommandProperty);
        set => SetValue(ProfileCommandProperty, value);
    }

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public Color DefaultColor
    {
        get => (Color)GetValue(DefaultColorProperty);
        set => SetValue(DefaultColorProperty, value);
    }

    public int ActiveTab
    {
        get => (int)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public Color HomeColor => ActiveTab == 0 ? SelectedColor : DefaultColor;

    public Color CategoriesColor => ActiveTab == 1 ? SelectedColor : DefaultColor;

    public Color OrdersColor => ActiveTab == 2 ? SelectedColor : DefaultColor;

    public Color ProfileColor => ActiveTab == 3 ? SelectedColor : DefaultColor;

    public DmBottomNav()
    {
        InitializeComponent();
    }

    private void RefreshColors()
    {
        OnPropertyChanged(nameof(HomeColor));
        OnPropertyChanged(nameof(CategoriesColor));
        OnPropertyChanged(nameof(OrdersColor));
        OnPropertyChanged(nameof(ProfileColor));
    }

    private static void OnActiveTabChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DmBottomNav nav)
        {
            nav.RefreshColors();
        }
    }

    private static void OnColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DmBottomNav nav)
        {
            nav.RefreshColors();
        }
    }
}
