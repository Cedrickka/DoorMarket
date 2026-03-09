# Phase 24 - Mobile Client Notification Feed (Flutter)

## Goal
Rendre l'ecran mobile `Notifications` utile pour les clients:
- notifications basees sur les vraies commandes,
- priorisation des incidents paiement,
- actions rapides vers commande/support.

## Delivered

### Mobile notifications (role-aware)
- L'ecran `/notifications` distingue maintenant:
  - `Admin/SuperAdmin`: centre operationnel existant (phase 23)
  - `Client/Shop`: feed client dynamique derive des commandes

### Feed client dynamique
- Chargement des commandes (`api/orders/mine`) puis generation d'evenements:
  - `payment failed`
  - `payment pending`
  - `payment paid`
  - `delivered`
  - `in progress`
- Affichage:
  - carte titre + horodatage + code commande
  - badge severite (`Critical`, `Action`, `Delivered`, etc.)
  - message contextuel (montant/statut)

### Actions UX
- Action primaire selon le cas:
  - paiement en erreur/en attente -> `Resume/Continue payment`
  - autres cas -> `View order`
- Action secondaire support (paiement failed/pending):
  - ouvre `/support` avec prefill (`subject`, `message`, `openForm=1`)
- `Pull-to-refresh` pour recharger les notifications client.

## Files
- `DoorMarket.Flutter/doormarket_flutter/lib/features/notifications/notifications_screen.dart`

## Validation
- `flutter analyze`
- `flutter test`

## Manual tests
1. Se connecter en client et ouvrir `/notifications`.
2. Verifier affichage d'un feed reel (si commandes existantes), sinon message vide.
3. Sur commande `payment failed`, verifier badge critique + bouton `Resume payment`.
4. Cliquer l'action primaire et verifier navigation vers `/orders/{id}`.
5. Cliquer `Support` (incident paiement) et verifier formulaire support pre-rempli.
6. Pull-to-refresh et verifier mise a jour du feed.
7. Se connecter en admin et verifier que le Notification Center admin reste inchangé.
