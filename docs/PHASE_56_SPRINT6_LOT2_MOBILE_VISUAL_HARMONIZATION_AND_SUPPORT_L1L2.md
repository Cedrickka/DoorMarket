# Phase 56 - Sprint 6 Lot 2: Flutter Visual Harmonization + Support L1/L2

## Scope
- Harmoniser les couleurs mobile ecran par ecran pour mode clair/sombre.
- Corriger les contrastes faibles (notamment profil/logos/icones en dark mode).
- Produire un guide operateur support L1/L2 detaille par incident.

## Flutter changes delivered

### 1) Profile and notifications dark-mode contrast
Updated:
- `DoorMarket.Flutter/doormarket_flutter/lib/features/profile/profile_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/notifications/notifications_screen.dart`

Done:
- Remplacement des couleurs fixes par couleurs adaptatives (`DmColors.*(isDark)`).
- Icones de profil/menu adaptees en dark mode.
- Badges et cartes notifications adaptes (read/unread + severite).
- Styles erreur/success adaptes dark/light.

### 2) Address and payments UX consistency
Updated:
- `DoorMarket.Flutter/doormarket_flutter/lib/features/addresses/address_form_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/addresses/addresses_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/payments/payments_screen.dart`

Done:
- Messages erreur lisibles en dark mode.
- Textes secondaires adaptes au theme.
- Icones de securite/paiement harmonisees.

### 3) Transaction screens harmonized
Updated:
- `DoorMarket.Flutter/doormarket_flutter/lib/features/orders/orders_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/orders/order_details_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/returns/returns_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/checkout/payment_return_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/cart/cart_screen.dart`

Done:
- Cohesion de styles badge/chips/status en dark/light.
- Textes muted et cartes info uniformises.
- Couleurs erreur/success conformes au theme actif.

### 4) Complementary screens adjusted
Updated:
- `DoorMarket.Flutter/doormarket_flutter/lib/features/auth/login_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/auth/register_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/auth/verify_email_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/support/support_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/settings/settings_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/home/home_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/categories/categories_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/checkout/checkout_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/shops/shop_products_screen.dart`

Done:
- Suppression des couleurs rouges/bleues fixes non adaptees.
- Uniformisation des textes secondaires et des icones utilitaires.

## Support documentation delivered
- New: `docs/DOORMARKET_SUPPORT_L1_L2_INCIDENT_PLAYBOOK.md`
  - 10 incidents standardises
  - actions L1
  - diagnostic L2
  - criteres d escalation et cloture

## Validation
Command executed:
```powershell
flutter analyze
```
Result:
- No issues found.

## Manual QA checklist
1. Mode sombre: ouvrir `Profil`, verifier contraste avatar/icones/menu.
2. Notifications: tester etats vide, unread, refresh, erreur reseau.
3. Adresses: creer/modifier/supprimer + selection zone livraison.
4. Paiements: verifier cartes provider, badges et lisibilite dark mode.
5. Panier/Commandes/Retours: verifier badges, status, aucun overflow texte.
6. Auth (login/register/verify): verifier message erreur lisible dark mode.
7. Home/Categories: verifier compteurs avis et textes secondaires lisibles.
