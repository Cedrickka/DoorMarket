# Phase 58 - Sprint 7: UI Performance + Accessibility + Checkout Hardening

## Objectifs
- Reduire les regressions de performance UI sur ecrans lourds.
- Renforcer l'accessibilite (semantics/tooltip/navigation).
- Durcir le parcours checkout multi-provider avec fallback automatique.

## 1) Performance UI - livrables

### Keep-alive sur ecrans lourds
Pour eviter des rebuilds complets lors de la navigation onglets:
- `HomeScreen` -> `AutomaticKeepAliveClientMixin`
- `CategoriesScreen` -> `AutomaticKeepAliveClientMixin`
- `ShopsScreen` -> `AutomaticKeepAliveClientMixin`
- `WishlistScreen` -> `AutomaticKeepAliveClientMixin`

Fichiers:
- `lib/features/home/home_screen.dart`
- `lib/features/categories/categories_screen.dart`
- `lib/features/shops/shops_screen.dart`
- `lib/features/wishlist/wishlist_screen.dart`

### Prefetch grid tuning
Ajout `cacheExtent` pour limiter le stutter au scroll:
- wishlist grid
- promotions grid
- categories products grid

Fichiers:
- `lib/features/wishlist/wishlist_screen.dart`
- `lib/features/promotions/promotions_screen.dart`
- `lib/features/categories/categories_screen.dart`

## 2) Accessibilite - livrables

### Boutons partages
Ajout de support:
- `semanticsLabel`
- `tooltip`
- wrapping `Semantics(button, enabled, label)`

Fichiers:
- `lib/core/widgets/dm_primary_button.dart`
- `lib/core/widgets/dm_ghost_button.dart`
- `lib/core/widgets/dm_secondary_button.dart`

### Navigation bas de page
- Semantics `selected` sur item actif
- Tooltip sur chaque item
- Semantics + tooltip sur bouton `+`

Fichier:
- `lib/core/widgets/dm_bottom_nav.dart`

### Header
- Semantics sur bouton retour/action
- Logo marque comme image accessible
- Interaction convertie en `InkWell` pour meilleure affordance

Fichier:
- `lib/core/widgets/dm_header.dart`

## 3) Checkout hardening multi-provider - livrables

### Nouveau service de resilience checkout
Nouveau composant:
- `lib/core/services/checkout_hardening_service.dart`

Fonctions:
- ordre de fallback externe Stripe/PayPal
- ordre de fallback Mobile Money (AIRTEL/ORANGE/MPESA)
- tentative ouverture checkout avec fallback et journal attempts
- resume des attempts pour message support/debug

### Checkout flow (creation commande)
- fallback automatique Stripe <-> PayPal si provider principal indisponible
- fallback Mobile Money provider si initiation echoue
- route `payment-return` utilise le provider reel utilise
- message warning enrichi avec details attempts en cas d'echec

Fichier:
- `lib/features/checkout/checkout_screen.dart`

### Order details flow (reprise paiement)
- fallback automatique Stripe <-> PayPal
- fallback Mobile Money provider + tracking provider utilise pour confirmation
- message succes uniformise pour eviter confusion provider

Fichier:
- `lib/features/orders/order_details_screen.dart`

### Payment return flow
- bouton "Rouvrir provider" avec fallback automatique Stripe/PayPal
- snackbar d'information si fallback effectue

Fichier:
- `lib/features/checkout/payment_return_screen.dart`

## 4) Cohesion theme
Ajout helper central pour couleur icone selon theme:
- `DmColors.iconFg(isDark)`

Fichier:
- `lib/core/theme/colors.dart`

## 5) Verification technique
Commandes executees:
```powershell
flutter analyze
flutter test
```
Resultat:
- analyze: no issues
- tests: all tests passed

## 6) Tests manuels recommandes
1. Checkout Stripe indisponible -> verifier bascule PayPal et route `payment-return` provider coherent.
2. Checkout PayPal indisponible -> verifier bascule Stripe.
3. Mobile Money: provider par defaut KO -> verifier fallback AIRTEL/ORANGE/MPESA.
4. Order details: reprise paiement depuis commande non payee, verifier fallback identique.
5. Payment return: bouton "Rouvrir" avec fallback + message utilisateur.
6. Accessibilite: lecteur d'ecran sur boutons principaux/nav bottom/header (labels announces).
7. Perf: alterner onglets Home/Categories/Shops/Wishlist et verifier conservation etat + fluidite scroll.
