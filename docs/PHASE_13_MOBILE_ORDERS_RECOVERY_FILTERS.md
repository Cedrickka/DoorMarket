# Phase 13 - Mobile Orders Recovery Filter and Refreshable List

## Goal
Make payment recovery easier from the orders list by exposing a dedicated pending-payments view and pull/refresh behavior.

## Delivered

### Flutter
- Orders list improvements:
  - pull-to-refresh (`RefreshIndicator`) bound to `ordersProvider`
  - new quick filter tab for `Payments to complete (N)`
  - empty-state message for the pending-payments view
- File:
  - `lib/features/orders/orders_screen.dart`

### MAUI
- Orders page improvements:
  - third tab added: pending payments (`OrdersPendingPaymentsTitle` with count)
  - dedicated list view for pending-payment orders
  - dedicated empty state for pending-payment tab
- Files:
  - `Pages/OrdersPage.xaml.cs`
  - `Pages/OrdersPage.xaml`
- Added localization keys (FR/EN):
  - `NoPendingPayments`
  - `NoPendingPaymentsHint`

## Validation
- `flutter analyze` -> OK
- `flutter test` -> OK
- `dotnet build DoorMarket.Mobile/...` -> OK
- `dotnet test DoorMarket.Tests/...` -> OK
