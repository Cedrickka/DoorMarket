# Phase 11 - Mobile Payment Resilience and Checkout Recovery

## Scope
Improve mobile payment resilience (Flutter + MAUI) by making pending payments explicit, adding restart actions, and hardening checkout against accidental double-submit.

## Implemented Changes

### Flutter
- `features/orders/orders_screen.dart`
  - Added a "payments to complete" banner with quick access to the first unpaid order.
  - Added inline warning on order cards when payment is not completed.
  - Improved ongoing/completed split to include cancelled/failed/completed statuses.

- `features/orders/order_details_screen.dart`
  - Added payment recovery UX for `pending` and `failed` statuses.
  - Added provider-aware recovery actions (PayPal or Stripe) and prepaid retry.
  - Added explicit "refresh status" action.
  - Added anti double-action lock while payment actions run.
  - Improved action feedback messages (success/error) and order refresh after prepaid payment.

- `features/checkout/checkout_screen.dart`
  - Added anti double-submit guard (`_submittingOrder`) on checkout action.
  - Kept navigation resilient when payment redirection fails: order is still created and user is redirected to order details to resume payment.
  - Added explicit warning message when payment is not finalized after order creation.

### MAUI
- `Pages/OrdersPage.xaml.cs` and `Pages/OrdersPage.xaml`
  - Added pending-payments summary block with one-tap resume action.
  - Added inline payment pending hint on order list items.

- `Pages/OrderDetailsPage.xaml.cs` and `Pages/OrderDetailsPage.xaml`
  - Added provider-aware primary action label (PayPal/Stripe).
  - Added explicit payment status refresh action.
  - Added differentiated recovery hints for `pending` and `failed` payment states.
  - Extended payment status mapping (`Pending`, `Failed`).

- `Pages/CheckoutPage.xaml.cs`
  - Added anti double-submit guard (`_isSubmittingOrder`).
  - Added recovery fallback messaging when order is created but payment is not completed.

- `Services/PaymentsApiClient.cs`
  - Added Stripe checkout session call for mobile recovery use case.

- `Models/OrderListItemViewModel.cs`
  - Added `NeedsPaymentAction` flag for list rendering.

- Localization keys added:
  - `Resources/Strings/AppResources.resx`
  - `Resources/Strings/AppResources.fr.resx`
  - `Resources/Strings/AppResources.en.resx`

## Validation
- `flutter analyze` -> OK (no issues)
- `flutter test` -> OK
- `dotnet build DoorMarket.Mobile/DoorMarket.Mobile.csproj -f net8.0-windows10.0.19041.0` -> OK
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj` -> OK

## Manual Test Checklist
1. Checkout (PayPal) creates order and opens provider.
2. If provider app/browser cannot open, verify user still lands on order details and sees recovery message.
3. Checkout button cannot trigger duplicate order creation with rapid repeated taps.
4. Orders list shows pending-payment banner when at least one order is unpaid/pending/failed.
5. Tapping "Resume payment" opens the targeted order details screen.
6. Order details shows correct payment status badge for Paid/Pending/Failed/Unpaid.
7. Provider action button label matches detected provider (PayPal or Stripe).
8. Prepaid retry works and refreshes status on success.
9. "Refresh status" updates payment state and hides recovery block once paid.
10. FR/EN labels render correctly for new texts.
