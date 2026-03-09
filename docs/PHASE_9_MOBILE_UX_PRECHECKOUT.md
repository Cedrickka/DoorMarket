# Phase 9 - Mobile UX Functionnelle (Pre-Checkout Explicite)

## Objectif
Aligner l'experience mobile avec le web sur la verification **pre-checkout**:
- afficher clairement les blocages avant checkout,
- afficher les avertissements utiles,
- bloquer l'action checkout si des conditions bloquantes existent.

## Portee
- Flutter (`DoorMarket.Flutter/doormarket_flutter`)
- MAUI (`DoorMarket.Mobile`)

## Modifications Flutter

### API et modeles
- Ajout des modeles pre-checkout dans:
  - [cart.dart](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Flutter/doormarket_flutter/lib/core/models/cart.dart)
  - `PreCheckoutRequest`
  - `CheckoutIssueDto`
  - `CheckoutReadinessDto`
- Ajout endpoint `api/cart/pre-checkout` dans:
  - [cart_api.dart](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Flutter/doormarket_flutter/lib/core/api/cart_api.dart)

### Etat panier
- Extension du state panier avec `readiness` et `readinessError`:
  - [cart_provider.dart](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Flutter/doormarket_flutter/lib/features/cart/cart_provider.dart)
- Chargement pre-checkout apres:
  - `load`, `addItem`, `updateItem`, `removeItem`
- Ajout de `refreshReadiness(...)` pour revalider apres application promo.

### UX panier
- Ecran mis a jour:
  - [cart_screen.dart](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Flutter/doormarket_flutter/lib/features/cart/cart_screen.dart)
- Ajouts:
  - panneau "Checkout bloque" + liste des `blockingIssues`,
  - panneau "Panier pret pour checkout" si aucun blocage,
  - panneau "Points a verifier" pour `warnings`,
  - fallback clair si verification indisponible.
- Le bouton checkout est **desactive** quand des blocages existent.
- Le resume (subtotal/delivery/discount/total/currency) utilise en priorite les valeurs `CheckoutReadinessDto`.

## Modifications MAUI

### API
- Ajout de `PreCheckoutAsync(...)` dans:
  - [CartApiClient.cs](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Services/CartApiClient.cs)

### Logique panier
- Ecran mis a jour:
  - [CartPage.xaml.cs](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Pages/CartPage.xaml.cs)
- Ajouts:
  - collections `BlockingIssues` et `CheckoutWarnings`,
  - etat readiness (`HasReadinessData`, `HasReadinessError`, etc.),
  - `CanCheckout` depend maintenant de l'absence de blocages,
  - `RefreshCheckoutReadinessAsync(...)` appelee apres recalcul panier/promo,
  - synchronisation des totaux depuis `CheckoutReadinessDto`,
  - message explicite si pre-checkout indisponible,
  - alerte detaillee si l'utilisateur tente checkout avec blocages.

### UI panier
- Ajout de blocs visuels readiness dans:
  - [CartPage.xaml](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Pages/CartPage.xaml)
  - carte blocages,
  - carte "ready",
  - carte warnings,
  - carte indisponibilite verification.
- Ajout d'icones support:
  - [IconGlyphs.cs](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Models/IconGlyphs.cs)

### Localisation
- Ajout de cles FR/EN pour messages pre-checkout:
  - [AppResources.resx](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Resources/Strings/AppResources.resx)
  - [AppResources.fr.resx](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Resources/Strings/AppResources.fr.resx)
  - [AppResources.en.resx](/c:/Users/Cedrick%20KANKOLONGO/source/repos/DoorMarket/DoorMarket.Mobile/Resources/Strings/AppResources.en.resx)

## Validation executee
- Flutter:
  - `flutter analyze` -> OK
  - `flutter test` -> OK
- MAUI:
  - `dotnet build DoorMarket.Mobile/DoorMarket.Mobile.csproj -f net8.0-windows10.0.19041.0` -> OK
- Backend tests (regression):
  - `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj` -> OK

## Checklist tests manuels

1. Cart avec stock/adresse invalides cote API:
- verifier affichage "Checkout bloque" avec liste des raisons,
- verifier bouton checkout desactive.

2. Cart valide:
- verifier affichage "Panier pret pour checkout",
- verifier bouton checkout actif.

3. Cart avec avertissements non bloquants:
- verifier affichage "Points a verifier",
- verifier checkout reste possible.

4. Application promo:
- verifier recalcul totals via readiness,
- verifier transitions blocked/ready apres promo.

5. Indisponibilite API pre-checkout (simulee):
- verifier message fallback visible,
- verifier UX reste comprehensible avant checkout.
