# Phase 27 - Mobile Notification Triage Mode (Flutter)

## Goal
Accélérer le traitement des alertes mobile avec un mode triage:
- ouverture directe en vue non lue,
- filtres opérationnels utiles,
- priorisation automatique des notifications critiques.

## Delivered

### Notification route improvements
- `/notifications` accepte maintenant `?unread=1`.
- L'écran `Notifications` démarre avec `Unread only` activé quand ce paramètre est présent.
- La navigation profil vers notifications ouvre désormais en mode triage (`/notifications?unread=1`).

### Home quick triage
- Le bouton cloche du Home ouvre automatiquement:
  - `/notifications?unread=1` si non lus > 0
  - `/notifications` sinon

### Client feed triage filters
- Nouveaux filtres dans la vue client notifications:
  - `Unread only`
  - `Actionable only`
  - catégories: `All`, `Payments`, `Delivery`, `Orders`
- Tri prioritaire ajouté:
  - non lus d'abord
  - criticité ensuite (`payment failed` > `payment pending` > `in progress` > `paid` > `delivered`)
  - date décroissante en dernier critère

## Files
- `DoorMarket.Flutter/doormarket_flutter/lib/core/router/app_router.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/home/home_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/notifications/notifications_screen.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/notifications/notification_feed_logic.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/notifications/notification_counters_provider.dart`

## Validation
- `flutter analyze`
- `flutter test`

## Manual tests
1. Depuis Home, avec non-lus > 0, cliquer la cloche et verifier ouverture en mode `Unread only`.
2. Depuis Profil > Notifications, verifier ouverture en mode triage.
3. Sur `/notifications`, tester les filtres `Actionable only` + categories.
4. Verifier que les notifications `payment failed` apparaissent avant les autres non lues.
5. Desactiver `Unread only` et verifier retour de tout le feed.
