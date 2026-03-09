# Phase 14 - Mobile Payment Support Prefill From Order Context

## Goal
Reduce friction when users need help on payment issues by opening support with a pre-filled subject/message directly from order details.

## Delivered

### Flutter
- Support route now accepts query params (`subject`, `message`, `openForm`).
- Support screen now supports prefilled subject/message and auto-opens contact form when prefill is provided.
- Order details "Need support help" action now opens support with payment context prefilled (order code, payment status, provider).
- Files:
  - `lib/core/router/app_router.dart`
  - `lib/features/support/support_screen.dart`
  - `lib/features/orders/order_details_screen.dart`

### MAUI
- Order details support action now navigates to `SupportPage` with encoded `subject/message/openForm` query parameters.
- Support page now implements `IQueryAttributable` and applies query prefill to contact form fields.
- Added localization keys for payment-issue support templates.
- Files:
  - `Pages/OrderDetailsPage.xaml.cs`
  - `Pages/SupportPage.xaml.cs`
  - `Resources/Strings/AppResources.resx`
  - `Resources/Strings/AppResources.fr.resx`
  - `Resources/Strings/AppResources.en.resx`

## Validation
- `flutter analyze` -> OK
- `flutter test` -> OK
- `dotnet build DoorMarket.Mobile/...` -> OK
- `dotnet test DoorMarket.Tests/... --no-build` -> OK
  - Note: regular `dotnet test` build step was blocked because `DoorMarket.Api.exe` was locked by a running local process.
