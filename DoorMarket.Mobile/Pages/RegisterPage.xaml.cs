using DoorMarket.Mobile.Localization;
using DoorMarket.Application.DTOs.Auth;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;

namespace DoorMarket.Mobile.Pages;

public partial class RegisterPage : LocalizedContentPage
{
    private readonly AuthApiClient _authApi;
    private readonly IServiceProvider _services;
    private readonly LanguageService _languageService;
    private bool _isBusy;

    public new bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public string CurrentLanguageFlagImage =>
        _languageService.CurrentLanguageCode == "en"
            ? "https://flagcdn.com/w20/us.png"
            : "https://flagcdn.com/w20/fr.png";

    public RegisterPage(AuthApiClient authApi, IServiceProvider services, LanguageService languageService)
    {
        InitializeComponent();
        _authApi = authApi;
        _services = services;
        _languageService = languageService;
        BindingContext = this;
    }

    protected override Task RefreshLanguageDataAsync()
    {
        OnPropertyChanged(nameof(CurrentLanguageFlagImage));
        return Task.CompletedTask;
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        if (IsBusy) return;
        SetError(null);

        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        var phone = PhoneEntry.Text?.Trim();
        var password = PasswordEntry.Text ?? string.Empty;
        var confirm = ConfirmPasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            SetError(AppText.Get("RegisterRequired"));
            return;
        }

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            SetError(AppText.Get("RegisterPasswordMismatch"));
            return;
        }

        IsBusy = true;
        try
        {
            await _authApi.RegisterAsync(new RegisterRequest(email, string.IsNullOrWhiteSpace(phone) ? null : phone, password), CancellationToken.None);

            await DisplayAlert(
                AppText.Get("EmailVerificationTitle"),
                AppText.Get("EmailVerificationMessage"),
                AppText.Get("Ok"));

            var code = await DisplayPromptAsync(
                AppText.Get("VerificationPromptTitle"),
                AppText.Get("VerificationPromptMessage"),
                AppText.Get("VerifyAction"),
                AppText.Get("LaterAction"),
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(code))
            {
                SetError(AppText.Get("VerificationPending"));
                return;
            }

            await _authApi.VerifyEmailAsync(new VerifyEmailRequest(email, code.Trim()), CancellationToken.None);
            await DisplayAlert(
                AppText.Get("SuccessTitle"),
                AppText.Get("VerificationSuccessLogin"),
                AppText.Get("Ok"));

            await NavigationHelper.PopAsync();
        }
        catch (ApiException ex)
        {
            SetError(ex.Message);
        }
        catch
        {
            SetError(AppText.Get("RegisterFailedGeneric"));
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

    private async void OnLanguageTapped(object? sender, TappedEventArgs e)
    {
        var french = $"???? {AppText.Get("LanguageFrench")}";
        var english = $"???? {AppText.Get("LanguageEnglish")}";
        var selected = await DisplayActionSheet(AppText.Get("SettingsLanguage"), AppText.Get("Cancel"), null, french, english);

        if (selected == french)
        {
            await _languageService.SetLanguageAsync("fr");
        }
        else if (selected == english)
        {
            await _languageService.SetLanguageAsync("en");
        }
    }

    private void SetError(string? message)
    {
        ErrorLabel.Text = message ?? string.Empty;
        ErrorLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }
}

