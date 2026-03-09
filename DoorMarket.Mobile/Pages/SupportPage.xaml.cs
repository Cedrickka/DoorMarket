using DoorMarket.Mobile.Localization;
using DoorMarket.Mobile.Services;
using DoorMarket.Mobile.Http;
using Microsoft.Maui.ApplicationModel;

namespace DoorMarket.Mobile.Pages;

public partial class SupportPage : LocalizedContentPage, IQueryAttributable
{
    private const string SupportEmail = "contact@door-market.com";
    private const string SupportPhoneDial = "+12202791506";

    private readonly SupportApiClient _supportApi;
    private bool _sending;

    public SupportPage(SupportApiClient supportApi)
    {
        _supportApi = supportApi;
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var subject = ReadQueryString(query, "subject");
        var message = ReadQueryString(query, "message");
        var openForm = ReadQueryString(query, "openForm");

        if (!string.IsNullOrWhiteSpace(subject))
        {
            ContactSubjectEntry.Text = subject;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            ContactMessageEditor.Text = message;
        }

        if (string.Equals(openForm, "1", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(subject) ||
            !string.IsNullOrWhiteSpace(message))
        {
            ContactFormCard.IsVisible = true;
        }
    }

    private async void OnEmailClicked(object sender, EventArgs e)
    {
        ContactFormCard.IsVisible = !ContactFormCard.IsVisible;
        if (ContactFormCard.IsVisible)
        {
            await HelpScroll.ScrollToAsync(ContactFormCard, ScrollToPosition.Start, true);
        }
    }

    private async void OnPhoneClicked(object sender, EventArgs e)
    {
        await OpenExternalUriAsync(
            $"tel:{SupportPhoneDial}",
            AppText.Get("PhoneOpenFailed"));
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await NavigationHelper.PopAsync();
    }

    private async void OnTocTapped(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable)
        {
            return;
        }

        var target = bindable.GetValue(TapGestureRecognizer.CommandParameterProperty) as string;
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        View? section = target switch
        {
            "intro" => IntroSection,
            "how" => HowSection,
            "products" => ProductsSection,
            "cart" => CartSection,
            "delivery" => DeliverySection,
            "payments" => PaymentsSection,
            "status" => StatusSection,
            "shop" => ShopSection,
            "faq" => FaqSection,
            "support" => SupportSection,
            _ => null
        };

        if (section is not null)
        {
            await HelpScroll.ScrollToAsync(section, ScrollToPosition.Start, true);
        }
    }

    private async Task OpenExternalUriAsync(string rawUri, string failureMessage)
    {
        try
        {
            if (!Uri.TryCreate(rawUri, UriKind.Absolute, out var uri))
            {
                await DisplayAlert(AppText.Get("HelpTitle"), failureMessage, AppText.Get("Ok"));
                return;
            }

            var canOpen = await Launcher.Default.CanOpenAsync(uri);
            if (!canOpen)
            {
                await DisplayAlert(AppText.Get("HelpTitle"), failureMessage, AppText.Get("Ok"));
                return;
            }

            await Launcher.Default.OpenAsync(uri);
        }
        catch (InvalidOperationException)
        {
            var fallbackMessage = rawUri.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                ? string.Format(AppText.Get("SupportContactEmail"), SupportEmail)
                : string.Format(AppText.Get("SupportContactPhone"), AppText.Get("HelpSupportPhone"));
            await DisplayAlert(AppText.Get("HelpTitle"), $"{failureMessage} {fallbackMessage}", AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("HelpTitle"), failureMessage, AppText.Get("Ok"));
        }
    }

    private async void OnSendContactClicked(object sender, EventArgs e)
    {
        if (_sending)
        {
            return;
        }

        var name = ContactNameEntry.Text?.Trim() ?? string.Empty;
        var email = ContactEmailEntry.Text?.Trim() ?? string.Empty;
        var subject = ContactSubjectEntry.Text?.Trim() ?? string.Empty;
        var message = ContactMessageEditor.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(message))
        {
            await DisplayAlert(AppText.Get("HelpTitle"), AppText.Get("SupportContactRequired"), AppText.Get("Ok"));
            return;
        }

        _sending = true;
        try
        {
            var payload = new SupportApiClient.SupportContactRequest(name, email, subject, message);
            await _supportApi.SendContactAsync(payload, CancellationToken.None);
            await DisplayAlert(AppText.Get("HelpTitle"), AppText.Get("SupportContactSent"), AppText.Get("Ok"));

            ContactMessageEditor.Text = string.Empty;
            ContactSubjectEntry.Text = string.Empty;
        }
        catch (ApiException ex)
        {
            await DisplayAlert(AppText.Get("HelpTitle"), ex.Message, AppText.Get("Ok"));
        }
        catch
        {
            await DisplayAlert(AppText.Get("HelpTitle"), AppText.Get("SupportContactFailed"), AppText.Get("Ok"));
        }
        finally
        {
            _sending = false;
        }
    }

    private static string ReadQueryString(IDictionary<string, object> query, string key)
    {
        if (!query.TryGetValue(key, out var raw) || raw is null)
        {
            return string.Empty;
        }

        var text = raw.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return Uri.UnescapeDataString(text).Trim();
    }
}
