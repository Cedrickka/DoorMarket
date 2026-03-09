# Phase 25 - Mobile Notification Preferences + Read State (Flutter)

## Goal
Rendre le centre notifications mobile plus exploitable au quotidien:
- preferences persistantes (types d'alertes),
- gestion lu/non-lu,
- meilleur tri de l'attention utilisateur.

## Delivered

### Preferences persistantes
- Nouveau controller `NotificationPreferencesController` (SharedPreferences):
  - activation globale notifications
  - `order updates`
  - `payment alerts`
  - `delivery alerts`
  - historique local des evenements lus

### Settings
- La section `Notifications` de `/settings` est maintenant branchee sur les vraies preferences:
  - switch activation globale
  - switches par categorie
  - compteur d'historique lu
  - action `Reset` pour vider les notifications lues

### Feed client notifications
- Le feed client applique maintenant les preferences:
  - masque les categories desactivees
  - message explicite si notifications desactivees
- Ajout du statut `Read/New` sur chaque carte.
- Ajout des controles rapides:
  - filtre `Unread only`
  - action `Mark all read`
- Quand l'utilisateur ouvre une commande/support depuis une notification,
  l'evenement est marque comme lu.

## Files
- `DoorMarket.Flutter/doormarket_flutter/lib/core/notifications/notification_preferences_controller.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/core/providers.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/settings/settings_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/notifications/notifications_screen.dart`

## Validation
- `flutter analyze`
- `flutter test`

## Manual tests
1. Ouvrir `/settings` -> `Notifications`, desactiver `Payment alerts`.
2. Ouvrir `/notifications` (client) et verifier que les alertes paiement disparaissent.
3. Reactiver `Payment alerts`, verifier retour des alertes.
4. Activer `Unread only`, verifier que seules les cartes `New` restent visibles.
5. Cliquer `Mark all read`, verifier passage en `Read` et compteur unread a zero.
6. Cliquer une notification (ouvrir commande/support), revenir et verifier qu'elle est `Read`.
7. Dans `/settings`, cliquer `Reset` et verifier que le statut lu est reinitialise.
