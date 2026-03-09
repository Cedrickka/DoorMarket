using DoorMarket.Mobile.Localization;
using DoorMarket.Application.DTOs.Delivery;
using DoorMarket.Application.DTOs.Me;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;

namespace DoorMarket.Mobile.Pages;

public partial class AddressFormPage : LocalizedContentPage
{
    private readonly AddressApiClient _api;
    private readonly DeliveryApiClient _deliveryApi;
    private List<DeliveryZoneDto> _zones = new();
    private AddressDto? _editing;
    private bool _isBusy;

    public event EventHandler? Saved;

    public new bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public AddressFormPage(AddressApiClient api, DeliveryApiClient deliveryApi)
    {
        InitializeComponent();
        _api = api;
        _deliveryApi = deliveryApi;
        BindingContext = this;
    }

    public void Load(AddressDto? address)
    {
        _editing = address;

        if (address == null)
        {
            TitleLabel.Text = AppText.Get("AddAddressTitle");
            LabelEntry.Text = AppText.Get("AddressLabelHome");
            FullNameEntry.Text = string.Empty;
            PhoneEntry.Text = string.Empty;
            CountryEntry.Text = "US";
            CityEntry.Text = string.Empty;
            DistrictEntry.Text = string.Empty;
            ZonePicker.SelectedItem = null;
            StreetEntry.Text = string.Empty;
            LandmarkEntry.Text = string.Empty;
            DefaultSwitch.IsToggled = false;
        }
        else
        {
            TitleLabel.Text = AppText.Get("EditAddressTitle");
            LabelEntry.Text = address.Label;
            FullNameEntry.Text = address.FullName;
            PhoneEntry.Text = address.Phone;
            CountryEntry.Text = address.Country;
            CityEntry.Text = address.City;
            DistrictEntry.Text = address.District;
            ZonePicker.SelectedItem = _zones.FirstOrDefault(z => z.Id == address.DeliveryZoneId);
            StreetEntry.Text = address.Street;
            LandmarkEntry.Text = address.Landmark;
            DefaultSwitch.IsToggled = address.IsDefault;
        }

        SetError(null);
    }

    protected override Task RefreshLanguageDataAsync()
    {
        Load(_editing);
        return Task.CompletedTask;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadZonesAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (IsBusy) return;
        SetError(null);

        var label = LabelEntry.Text?.Trim();
        var fullName = FullNameEntry.Text?.Trim();
        var phone = PhoneEntry.Text?.Trim();
        var country = CountryEntry.Text?.Trim() ?? "US";
        var city = CityEntry.Text?.Trim();
        var district = DistrictEntry.Text?.Trim();
        var selectedZone = ZonePicker.SelectedItem as DeliveryZoneDto;
        var street = StreetEntry.Text?.Trim();
        var landmark = string.IsNullOrWhiteSpace(LandmarkEntry.Text) ? null : LandmarkEntry.Text.Trim();
        var isDefault = DefaultSwitch.IsToggled;

        if (string.IsNullOrWhiteSpace(label))
        {
            label = AppText.Get("AddressLabelHome");
        }

        if (string.IsNullOrWhiteSpace(fullName)
            || string.IsNullOrWhiteSpace(phone)
            || string.IsNullOrWhiteSpace(city)
            || string.IsNullOrWhiteSpace(district)
            || string.IsNullOrWhiteSpace(street))
        {
            SetError(AppText.Get("AddressRequiredFields"));
            return;
        }

        if (selectedZone is null)
        {
            SetError(AppText.Get("DeliveryZoneRequired"));
            return;
        }

        IsBusy = true;
        try
        {
            if (_editing == null)
            {
                var req = new CreateAddressRequest(label, fullName, phone, country, city, district, selectedZone?.Id, street, landmark, isDefault);
                await _api.CreateAsync(req, CancellationToken.None);
            }
            else
            {
                var req = new UpdateAddressRequest(label, fullName, phone, country, city, district, selectedZone?.Id, street, landmark, isDefault);
                await _api.UpdateAsync(_editing.Id, req, CancellationToken.None);
            }

            Saved?.Invoke(this, EventArgs.Empty);
            await NavigationHelper.PopAsync();
        }
        catch (ApiException ex)
        {
            SetError(ex.Message);
        }
        catch
        {
            SetError(AppText.Get("SaveAddressFailed"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PopAsync();
    }

    private void SetError(string? message)
    {
        ErrorLabel.Text = message ?? string.Empty;
        ErrorLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private async Task LoadZonesAsync()
    {
        try
        {
            _zones = await _deliveryApi.GetZonesAsync(CancellationToken.None);
            ZonePicker.ItemsSource = _zones;
            ZonePicker.ItemDisplayBinding = new Binding(nameof(DeliveryZoneDto.Name));

            if (_editing is not null && _editing.DeliveryZoneId.HasValue)
            {
                ZonePicker.SelectedItem = _zones.FirstOrDefault(z => z.Id == _editing.DeliveryZoneId.Value);
            }
        }
        catch
        {
            _zones = new();
            ZonePicker.ItemsSource = _zones;
        }
    }
}
