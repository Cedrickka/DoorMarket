# DM60 - Branding QA Smoke (Web + Flutter + MAUI)

## Scope
- Verify that new branding assets are visible and consistent after integration.
- Validate light/dark readability and fallback behavior.
- Confirm no runtime regressions related to replaced assets.

## Quick technical pre-check
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj`
- `flutter analyze` (in `DoorMarket.Flutter/doormarket_flutter`)
- `dotnet build DoorMarket.Mobile/DoorMarket.Mobile.csproj`

## Web smoke checklist (5 min)
1. Open web app and verify sidebar/header logo:
   - expected asset: `/images/logo-doormarket.png`
   - location: main layout branding block.
2. Verify browser tab icon:
   - expected asset: `/favicon.png`
3. Open profile page with a user without profile picture:
   - expected fallback icon: `/favicon.png`
4. Validate dark mode readability:
   - logo remains visible on dark blue sidebar/header.
5. Confirm no stale duplicate file is served:
   - source file `wwwroot/images/logo-doormarket.png.png` must not exist.

## Flutter smoke checklist (8 min)
1. Login screen:
   - logo displayed from `assets/images/door_market_logo_mobile.png`
2. Register screen:
   - same logo and correct scaling.
3. Home header:
   - logo visible, no clipping/overflow.
4. Splash:
   - logo appears centered, no pixelation.
5. About + Not found pages:
   - same logo consistency.
6. Payments screen:
   - confirm payment method assets exist in bundle:
     - `payment_stripe.png`
     - `payment_paypal.png`
     - `payment_mobile_money.png`
     - `payment_prepaid.png`
     - `payment_cod.png`
   - if UI still uses icons only, record as UX backlog item (not a blocking bug).
7. Flutter web/PWA:
   - verify favicon and app icons updated (`web/favicon.png`, `web/icons/*`).

## MAUI smoke checklist (8 min)
1. Splash:
   - expected source: `Resources/Splash/splash.svg`
2. App icon:
   - expected source: `Resources/AppIcon/appicon.png`
3. Login/Register:
   - expected source: `doormarket_logo_mobile_small.png`
4. Splash/Home/Profile/Categories headers:
   - expected source: `doormarket_icon_64.png` where used.
5. Cart fallback image:
   - expected fallback source: `doormarket_icon_256.png`

## Visual acceptance criteria
- No stretched logo.
- No clipped logo.
- No hardcoded old/placeholder icon.
- Contrast remains readable in dark mode.
- No red-screen/layout assertion caused by header/logo blocks.

## Known non-blocking caveat
- Old references to `logo-doormarket.png.png` can still appear in generated build artifacts (`obj/`, `artifacts/`) from historical outputs. Source code is cleaned; regenerate artifacts as needed.

