# Phase 15 - Mobile Payment Return Flow With Explicit Result Screen

## Goal
Deliver an explicit end-to-end return flow after external payment redirects (PayPal/Stripe), with automatic status verification and clear next actions.

## Delivered

### Flutter
- Added a dedicated payment-return screen:
  - route: `/payment-return/:id`
  - auto-polling payment status every 10s
  - refresh on app resume
  - explicit result states: paid / pending / failed
  - actions: check now, reopen provider, contact support (prefilled), open order details, back to orders
- Checkout now redirects to payment-return flow for PayPal/Stripe after order creation.
- Support route now accepts prefill query params and support form auto-opens when prefilled.
- Files:
  - `lib/features/checkout/payment_return_screen.dart`
  - `lib/features/checkout/checkout_screen.dart`
  - `lib/core/router/app_router.dart`
  - `lib/features/support/support_screen.dart`
  - `lib/features/orders/order_details_screen.dart`

### MAUI
- Added dedicated payment-return page with explicit states and actions:
  - auto-polling every 10s until terminal state
  - actions: check now, reopen provider, contact support (prefilled), open order details, back to orders
- Checkout now opens payment-return page for PayPal flows.
- Order details now redirects to payment-return page after reopening PayPal/Stripe.
- Added support prefill templates and payment-return i18n keys (FR/EN).
- Files:
  - `Pages/PaymentReturnPage.xaml`
  - `Pages/PaymentReturnPage.xaml.cs`
  - `Pages/CheckoutPage.xaml.cs`
  - `Pages/OrderDetailsPage.xaml.cs`
  - `Pages/SupportPage.xaml.cs`
  - `MauiProgram.cs`
  - `AppShell.xaml.cs`
  - `Resources/Strings/AppResources.resx`
  - `Resources/Strings/AppResources.fr.resx`
  - `Resources/Strings/AppResources.en.resx`

## Validation
- `flutter analyze` -> OK
- `flutter test` -> OK
- `dotnet build DoorMarket.Mobile/...` -> OK
- `dotnet test DoorMarket.Tests/... --no-build` -> OK

## Manual Tests
1. Create checkout with PayPal and validate navigation to payment-return screen.
2. Pay externally, return to app, verify auto status transitions to Paid.
3. Pending/failed case: verify “Re-open provider” works.
4. Verify “Contact support” opens support with prefilled subject/message.
5. Verify “Open order details” opens target order.
6. Verify FR/EN labels for payment-return and support prefill flow.
