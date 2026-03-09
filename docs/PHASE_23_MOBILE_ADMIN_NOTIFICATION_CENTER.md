# Phase 23 - Mobile Admin Notification Center (Flutter)

## Goal
Apporter sur mobile Flutter une vue operationnelle des notifications transactionnelles pour les admins, coherente avec le web:
- suivi des incidents,
- retry rapide,
- workflow acknowledge/reopen.

## Delivered

### Flutter API layer
- Nouveau client API admin notifications:
  - `getSummary`
  - `getIncidents`
  - `getTransactions`
  - `retryOne`
  - `retryFailed`
  - `acknowledgeIncident`
  - `reopenIncident`
- Nouveaux DTOs mobile pour summary/incidents/transactions/retry/ack.

### Flutter UI `/notifications`
- Ecran `Notifications` evolue en mode role-aware:
  - role `Admin/SuperAdmin` => Notification Center operationnel
  - autres roles => vue client simplifiee existante
- Fonctions admin ajoutees:
  - filtres `type`, `status`, `min failures`, `max incidents`
  - switch `show acknowledged`
  - section summary par type
  - section incidents avec actions:
    - `Retry`
    - `Acknowledge`
    - `Reopen`
  - section transactions paginee (mobile-friendly)
  - action `Bulk retry`
  - `pull-to-refresh`

### Navigation profile
- Ajout entree `Notifications` dans le menu Profile.

## Files
- `DoorMarket.Flutter/doormarket_flutter/lib/core/models/admin_notifications.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/core/api/admin_notifications_api.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/core/providers.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/notifications/notifications_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/profile/profile_screen.dart`

## Validation
- `flutter analyze`
- `flutter test`

## Manual tests
1. Se connecter avec un compte admin et ouvrir `/notifications`.
2. Verifier le chargement des sections `summary`, `incidents`, `transactions`.
3. Tester `Retry` sur un incident, verifier snackbar + refresh data.
4. Tester `Acknowledge`, verifier disparition de l'incident si `show acknowledged` est desactive.
5. Activer `show acknowledged`, verifier affichage des incidents acquittes.
6. Tester `Reopen`, verifier retour de l'incident en vue active.
7. Tester `Bulk retry` et verifier mise a jour des compteurs.
8. Se connecter en compte client, verifier que la vue simple notifications reste affichee.
