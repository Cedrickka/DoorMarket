using DoorMarket.Mobile.Auth;
using DoorMarket.Mobile.Services;
using DoorMarket.Mobile.Pages;
using System.Diagnostics;

namespace DoorMarket.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;
    private readonly AuthState _authState;
    private readonly AuthSession _session;
    private readonly LanguageService _languageService;
    private Window? _window;
    private bool _isApplyingRoot;
    private bool _forceRootReload;

    public App(IServiceProvider services, AuthState authState, AuthSession session, LanguageService languageService)
    {
        InitializeComponent();
        _services = services;
        _authState = authState;
        _session = session;
        _languageService = languageService;
        _languageService.Initialize();

        DoorMarket.Mobile.Services.ThemeManager.ApplySavedTheme();
        _authState.Changed += OnAuthStateChanged;

        // Prevent background task faults from terminating the app silently.
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _window = new Window(_services.GetRequiredService<SplashPage>());
        _ = InitializeAsync();
        return _window;
    }

    private void OnAuthStateChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(SetRootPageForAuthState);
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _session.InitializeAsync();
        }
        catch
        {
            // Fall back to login page when auth bootstrap fails.
        }

        MainThread.BeginInvokeOnMainThread(SetRootPageForAuthState);
    }

    private void SetRootPageForAuthState()
    {
        if (_window is null || _isApplyingRoot)
        {
            return;
        }

        _isApplyingRoot = true;

        try
        {
            if (_authState.IsAuthenticated)
            {
                if (_window.Page is AppShell && !_forceRootReload)
                {
                    return;
                }

                _window.Page = _services.GetRequiredService<AppShell>();
                return;
            }

            if (_window.Page is NavigationPage navigationPage && navigationPage.RootPage is LoginPage && !_forceRootReload)
            {
                return;
            }

            _window.Page = new NavigationPage(_services.GetRequiredService<LoginPage>());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DoorMarket] Root page switch error: {ex}");
            try
            {
                _window.Page = new NavigationPage(_services.GetRequiredService<LoginPage>());
            }
            catch
            {
                // Leave current page as-is if fallback also fails.
            }
        }
        finally
        {
            _forceRootReload = false;
            _isApplyingRoot = false;
        }
    }

    public void ReloadRootForLanguageChange()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _forceRootReload = true;
            SetRootPageForAuthState();
        });
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Debug.WriteLine($"[DoorMarket] Unobserved task exception: {e.Exception}");
        e.SetObserved();
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[DoorMarket] Unhandled exception: {e.ExceptionObject}");
    }
}
