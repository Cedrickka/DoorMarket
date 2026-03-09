using DoorMarket.Mobile.Localization;
using DoorMarket.Application.DTOs.Me;
using DoorMarket.Mobile.Auth;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;
using DoorMarket.Mobile.Utils;
using Microsoft.Maui.Media;
using System.IO;

namespace DoorMarket.Mobile.Pages;

public partial class ProfilePage : LocalizedContentPage
{
    private readonly MeApiClient _meApi;
    private readonly AuthSession _session;
    private readonly IServiceProvider _services;
    private bool _loaded;
    private bool _isBusy;
    private MeDto? _me;
    private string? _profileImageUrl;

    public new bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public ProfilePage(MeApiClient meApi, AuthSession session, IServiceProvider services)
    {
        InitializeComponent();
        _meApi = meApi;
        _session = session;
        _services = services;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var me = await _meApi.GetMeAsync(CancellationToken.None);
            _me = me;
            ApplyMe(me);
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("ProfileLoadFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyMe(MeDto me)
    {
        EmailLabel.Text = me.Email;
        PhoneEntry.Text = me.Phone ?? string.Empty;
        RoleLabel.Text = me.Role;
        StatusLabel.Text = me.IsActive ? AppText.Get("Active") : AppText.Get("Inactive");
        _profileImageUrl = string.IsNullOrWhiteSpace(me.ProfileImageUrl)
            ? MediaCatalog.Profile(me.Email)
            : me.ProfileImageUrl;
        ProfilePhoto.Source = _profileImageUrl;
    }

    protected override Task RefreshLanguageDataAsync()
    {
        if (_me is not null)
        {
            ApplyMe(_me);
        }

        return Task.CompletedTask;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var phone = string.IsNullOrWhiteSpace(PhoneEntry.Text) ? null : PhoneEntry.Text.Trim();
            await _meApi.RequestPhoneChangeAsync(new RequestPhoneChangeRequest(phone), CancellationToken.None);

            await DisplayAlert(
                AppText.Get("OtpTitle"),
                AppText.Get("OtpProfilePhoneMessage"),
                AppText.Get("Ok"));

            var code = await DisplayPromptAsync(
                AppText.Get("OtpPromptTitle"),
                AppText.Get("OtpPromptMessage"),
                AppText.Get("VerifyAction"),
                AppText.Get("Cancel"),
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(code))
            {
                await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("OtpRequired"), AppText.Get("Ok"));
                return;
            }

            await _meApi.ConfirmPhoneChangeAsync(new ConfirmPhoneChangeRequest(code.Trim()), CancellationToken.None);
            await DisplayAlert(AppText.Get("SuccessTitle"), AppText.Get("ProfileUpdated"), AppText.Get("Ok"));
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("ProfileUpdateFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnChangePasswordClicked(object sender, EventArgs e)
    {
        if (IsBusy) return;

        var password = NewPasswordEntry.Text ?? string.Empty;
        var confirm = ConfirmPasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("PasswordRequired"), AppText.Get("Ok"));
            return;
        }

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("RegisterPasswordMismatch"), AppText.Get("Ok"));
            return;
        }

        IsBusy = true;
        try
        {
            await _meApi.RequestPasswordChangeAsync(new RequestPasswordChangeRequest(password), CancellationToken.None);

            await DisplayAlert(
                AppText.Get("OtpTitle"),
                AppText.Get("OtpProfilePasswordMessage"),
                AppText.Get("Ok"));

            var code = await DisplayPromptAsync(
                AppText.Get("OtpPromptTitle"),
                AppText.Get("OtpPromptMessage"),
                AppText.Get("VerifyAction"),
                AppText.Get("Cancel"),
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(code))
            {
                await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("OtpRequired"), AppText.Get("Ok"));
                return;
            }

            await _meApi.ConfirmPasswordChangeAsync(new ConfirmPasswordChangeRequest(code.Trim()), CancellationToken.None);
            await DisplayAlert(AppText.Get("SuccessTitle"), AppText.Get("PasswordUpdated"), AppText.Get("Ok"));

            NewPasswordEntry.Text = string.Empty;
            ConfirmPasswordEntry.Text = string.Empty;

            await _session.SignOutAsync();
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("ProfileUpdateFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnChangePhotoClicked(object sender, EventArgs e)
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            var photo = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
            {
                Title = AppText.Get("ChangePhotoAction")
            });

            if (photo is null)
            {
                return;
            }

            await using var stream = await photo.OpenReadAsync();
            var contentType = string.IsNullOrWhiteSpace(photo.ContentType)
                ? GuessContentType(photo.FileName)
                : photo.ContentType;

            var response = await _meApi.UploadProfilePhotoAsync(stream, photo.FileName, contentType, CancellationToken.None);
            _profileImageUrl = response.Url;
            ProfilePhoto.Source = _profileImageUrl;

            if (_me is not null)
            {
                _me = _me with { ProfileImageUrl = _profileImageUrl };
            }

            await DisplayAlert(AppText.Get("SuccessTitle"), AppText.Get("PhotoUpdated"), AppText.Get("Ok"));
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("ErrorTitle"), AppText.Get("PhotoUpdateFailed"), AppText.Get("Ok"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string GuessContentType(string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }

    private async void OnAddressesClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PushAsync(_services.GetRequiredService<AddressesPage>());
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await _session.SignOutAsync();
    }

    private async void OnPaymentsClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PushAsync(_services.GetRequiredService<PaymentsPage>());
    }

    private async void OnSupportClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PushAsync(_services.GetRequiredService<SupportPage>());
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PushAsync(_services.GetRequiredService<SettingsPage>());
    }
}


