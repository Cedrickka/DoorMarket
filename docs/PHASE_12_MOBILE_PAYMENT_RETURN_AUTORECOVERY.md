# Phase 12 - Mobile Payment Return and Auto-Recovery Loop

## Goal
Strengthen post-checkout confidence by making payment return handling explicit on mobile, including periodic status refresh and fast support escalation.

## Delivered

### Flutter
- Order details now includes:
  - automatic payment-status refresh every 12 seconds while payment is not `Paid`
  - refresh on app resume (after external provider return)
  - explicit support action (`/support`) in payment recovery block
  - last auto-refresh timestamp display
- File:
  - `lib/features/orders/order_details_screen.dart`

### MAUI
- Order details now includes:
  - automatic payment-status polling loop every 12 seconds while payment is not `Paid`
  - support action button from payment recovery block
  - recovery hint text with auto-refresh status/time
- Files:
  - `Pages/OrderDetailsPage.xaml.cs`
  - `Pages/OrderDetailsPage.xaml`
- Added localization keys (FR/EN):
  - `NeedSupportAction`
  - `PaymentAutoRefreshHint`
  - `PaymentAutoRefreshWithTimeHint`
  - `NavigationOpenSupportFailed`

## Validation
- `flutter analyze` -> OK
- `flutter test` -> OK
- `dotnet build DoorMarket.Mobile/...` -> OK
- `dotnet test DoorMarket.Tests/...` -> OK
