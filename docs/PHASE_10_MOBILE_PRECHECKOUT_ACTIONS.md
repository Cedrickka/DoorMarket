# Phase 10 - Mobile Pre-Checkout Correction Loop

## Goal
Reduce checkout friction by adding direct correction actions from pre-checkout issues:
- explicit issue -> explicit action,
- quick navigation to fix screens,
- immediate readiness refresh after returning.

## Implemented

### Flutter
- Issue-code action mapping in cart:
  - address/zone issues -> open addresses
  - prepaid payment issue -> open payments
  - catalog/stock/product/shop issues -> open catalog
  - unknown or readiness error -> retry validation
- Recommended actions card added in cart UI.
- Checkout navigation now refreshes cart/readiness on return.

Files:
- [cart_screen.dart](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Flutter/doormarket_flutter/lib/features/cart/cart_screen.dart)

### MAUI
- Issue-code action mapping state in cart.
- Recommended actions card with contextual buttons:
  - manage addresses
  - payments
  - catalog
  - retry
- Clear blocked-checkout alert content using issue list.
- Navigation handlers for correction actions.

Files:
- [CartPage.xaml.cs](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Pages/CartPage.xaml.cs)
- [CartPage.xaml](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Pages/CartPage.xaml)

### Localization
Added action/feedback keys for this correction loop:
- [AppResources.resx](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Resources/Strings/AppResources.resx)
- [AppResources.fr.resx](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Resources/Strings/AppResources.fr.resx)
- [AppResources.en.resx](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Resources/Strings/AppResources.en.resx)

## Validation
- `flutter analyze` -> OK
- `flutter test` -> OK
- `dotnet build DoorMarket.Mobile/DoorMarket.Mobile.csproj -f net8.0-windows10.0.19041.0` -> OK
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj` -> OK

## Manual checks
1. Force `delivery_zone_missing` warning:
- cart shows addresses action,
- opening addresses and returning refreshes readiness.

2. Force `prepaid_code_required` block:
- cart shows payments action,
- checkout button disabled until issue is resolved.

3. Force `stock_insufficient`/`product_inactive` block:
- cart shows catalog action.

4. Simulate pre-checkout API failure:
- retry action is shown and triggers a fresh load.
