using DoorMarket.Mobile.Localization;
using DoorMarket.Application.DTOs.Auth;
using DoorMarket.Mobile.Auth;
using DoorMarket.Mobile.Http;
using DoorMarket.Mobile.Services;

namespace DoorMarket.Mobile.Pages;

public partial class LoginPage : LocalizedContentPage
{
    private readonly AuthApiClient _authApi;
    private readonly AuthSession _session;
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

    public LoginPage(AuthApiClient authApi, AuthSession session, IServiceProvider services, LanguageService languageService)
    {
        InitializeComponent();
        _authApi = authApi;
        _session = session;
        _services = services;
        _languageService = languageService;
        BindingContext = this;
    }

    protected override Task RefreshLanguageDataAsync()
    {
        OnPropertyChanged(nameof(CurrentLanguageFlagImage));
        return Task.CompletedTask;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (IsBusy) return;
        SetError(null);

        var login = LoginEntry.Text?.Trim();
        var password = PasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            SetError(AppText.Get("LoginMissingCredentials"));
            return;
        }

        IsBusy = true;
        try
        {
            var response = await _authApi.LoginAsync(new LoginRequest(login, password), CancellationToken.None);
            if (response.OtpRequired)
            {
                SetError(AppText.Get("OtpLoginDisabled"));
                return;
            }

            await _session.SignInAsync(response);
        }
        catch (ApiException ex)
        {
            SetError(ex.Message);
        }
        catch
        {
            SetError(AppText.Get("LoginFailedGeneric"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PushAsync(_services.GetRequiredService<RegisterPage>());
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

