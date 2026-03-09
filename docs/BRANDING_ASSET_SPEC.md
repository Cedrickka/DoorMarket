# DoorMarket - Branding Asset Spec

Ce document definit les fichiers de branding a fournir pour remplacer les logos actuels sur Web, Flutter et MAUI.

## 1) Dossier de depot (source unique)

Deposer les fichiers dans:

- `branding/incoming/logo/`
- `branding/incoming/app/`
- `branding/incoming/payment/`

## 2) Fichiers a fournir (masters)

### Logo marque

1. `branding/incoming/logo/dm_logo_mark.svg`
- Usage: logo carre (icone, favicon, app icon, pills d'entete).
- Format: SVG vectoriel (obligatoire) + `dm_logo_mark.png` en fallback.
- Taille PNG fallback: 1024 x 1024 px, fond transparent.

2. `branding/incoming/logo/dm_logo_wordmark.svg`
- Usage: logo horizontal (auth headers, splash branding, web branding large).
- Format: SVG vectoriel (obligatoire) + `dm_logo_wordmark.png` en fallback.
- Taille PNG fallback: 2400 x 720 px (ratio ~3.33:1), fond transparent.

3. `branding/incoming/logo/dm_logo_wordmark_light.svg`
- Meme logo horizontal optimise pour fond sombre.
- Format: SVG + PNG fallback 2400 x 720.

### App / PWA

4. `branding/incoming/app/dm_app_icon_1024.png`
- Usage: base app stores iOS/Android/Windows/PWA.
- Taille: 1024 x 1024 px exact.
- Format: PNG, fond plein (pas de transparence pour stores).

5. `branding/incoming/app/dm_favicon_64.png`
- Usage: favicon Web.
- Taille: 64 x 64 px.
- Format: PNG.

6. `branding/incoming/app/dm_splash_logo.svg`
- Usage: splash MAUI / Flutter.
- Format: SVG (preferred), avec fallback PNG.
- Fallback PNG: 1024 x 1024 px transparent.

### Paiement (logos officiels)

7. `branding/incoming/payment/payment_stripe.svg`
8. `branding/incoming/payment/payment_paypal.svg`
9. `branding/incoming/payment/payment_mobile_money.svg`
10. `branding/incoming/payment/payment_prepaid.svg`
11. `branding/incoming/payment/payment_cod.svg`
- Format: SVG (preferred) + PNG fallback.
- Taille PNG fallback: 600 x 180 px, fond transparent.

## 3) Contraintes qualite

- Espace colorimetrique: sRGB.
- Eviter JPG pour logos (sauf photos marketing).
- Pas de fond blanc force sur logo.
- Marges internes propres (pas de logo colle aux bords).
- Conserver une zone de protection autour du logo.

## 4) Mapping vers les fichiers du projet

### Web (Blazor)

- `DoorMarket.Web/wwwroot/images/logo-doormarket.png` <= `dm_logo_mark` (PNG 512 x 512)
- `DoorMarket.Web/wwwroot/favicon.png` <= `dm_favicon_64.png`

Note: `DoorMarket.Web/wwwroot/images/logo-doormarket.png.png` est un doublon a supprimer a la phase d'integration.

### Flutter

- `DoorMarket.Flutter/doormarket_flutter/assets/images/door_market_logo_mobile.png`
  - Ideal: remplacer par wordmark horizontal (2400 x 720) et separer les usages iconiques avec `dm_logo_mark`.
- `DoorMarket.Flutter/doormarket_flutter/assets/images/payment_stripe.png`
- `DoorMarket.Flutter/doormarket_flutter/assets/images/payment_paypal.png`
- `DoorMarket.Flutter/doormarket_flutter/assets/images/payment_mobile_money.png`
- `DoorMarket.Flutter/doormarket_flutter/assets/images/payment_prepaid.png`
- `DoorMarket.Flutter/doormarket_flutter/assets/images/payment_cod.png`

PWA / Web Flutter:

- `DoorMarket.Flutter/doormarket_flutter/web/favicon.png` <= `dm_favicon_64.png` (export 16/32/64)
- `DoorMarket.Flutter/doormarket_flutter/web/icons/Icon-192.png`
- `DoorMarket.Flutter/doormarket_flutter/web/icons/Icon-512.png`
- `DoorMarket.Flutter/doormarket_flutter/web/icons/Icon-maskable-192.png`
- `DoorMarket.Flutter/doormarket_flutter/web/icons/Icon-maskable-512.png`
  - Tous derives de `dm_app_icon_1024.png`.

### Mobile MAUI

- `DoorMarket.Mobile/Resources/AppIcon/appicon.png` <= `dm_app_icon_1024.png`
- `DoorMarket.Mobile/Resources/Splash/splash.svg` <= `dm_splash_logo.svg`
- `DoorMarket.Mobile/Resources/Images/doormarket_logo_mobile.png` <= `dm_logo_wordmark` (600 x 180)
- `DoorMarket.Mobile/Resources/Images/doormarket_logo_mobile_small.png` <= `dm_logo_wordmark` (300 x 90)
- `DoorMarket.Mobile/Resources/Images/doormarket_icon_256.png` <= `dm_logo_mark` (256 x 256)
- `DoorMarket.Mobile/Resources/Images/doormarket_icon_64.png` <= `dm_logo_mark` (64 x 64)

## 5) Etape suivante

Quand tu as depose ces fichiers dans `branding/incoming/`, je fais directement:

1. generation des derives manquants,
2. remplacement dans Web + Flutter + MAUI,
3. nettoyage des doublons,
4. verification visuelle light/dark.
