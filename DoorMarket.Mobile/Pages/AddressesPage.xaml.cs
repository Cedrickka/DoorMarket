using DoorMarket.Mobile.Localization;
using System.Collections.ObjectModel;
using DoorMarket.Application.DTOs.Me;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;

namespace DoorMarket.Mobile.Pages;

public partial class AddressesPage : LocalizedContentPage
{
    private readonly AddressApiClient _api;
    private readonly IServiceProvider _services;
    private bool _isBusy;

    public ObservableCollection<AddressDto> Addresses { get; } = new();

    public new bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public AddressesPage(AddressApiClient api, IServiceProvider services)
    {
        InitializeComponent();
        _api = api;
        _services = services;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    protected override async Task RefreshLanguageDataAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var items = await _api.GetAddressesAsync(CancellationToken.None);
            Addresses.Clear();
            foreach (var item in items)
                Addresses.Add(item);
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("LoadAddressesFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        var page = _services.GetRequiredService<AddressFormPage>();
        page.Load(null);
        page.Saved += OnFormSaved;
        await NavigationHelper.PushAsync(page);
    }

    private async void OnEditInvoked(object sender, EventArgs e)
    {
        if (sender is not SwipeItem swipe || swipe.CommandParameter is not AddressDto address)
            return;

        await OpenEditFormAsync(address);
    }

    private async void OnDeleteInvoked(object sender, EventArgs e)
    {
        if (sender is not SwipeItem swipe || swipe.CommandParameter is not AddressDto address)
            return;

        await DeleteAddressAsync(address);
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: AddressDto address })
        {
            return;
        }

        await OpenEditFormAsync(address);
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: AddressDto address })
        {
            return;
        }

        await DeleteAddressAsync(address);
    }

    private async Task OpenEditFormAsync(AddressDto address)
    {
        var page = _services.GetRequiredService<AddressFormPage>();
        page.Load(address);
        page.Saved += OnFormSaved;
        await NavigationHelper.PushAsync(page);
    }

    private async Task DeleteAddressAsync(AddressDto address)
    {
        var confirm = await DisplayAlert(
            AppText.Get("Delete"),
            AppText.Get("ConfirmDeleteAddress"),
            AppText.Get("Yes"),
            AppText.Get("No"));
        if (!confirm) return;

        IsBusy = true;
        try
        {
            await _api.DeleteAsync(address.Id, CancellationToken.None);
            Addresses.Remove(address);
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("DeleteAddressFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnFormSaved(object? sender, EventArgs e)
    {
        if (sender is AddressFormPage form)
            form.Saved -= OnFormSaved;

        await LoadAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PopAsync();
    }
}

