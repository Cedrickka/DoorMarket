# Phase 29 - Mobile Notification Offline Cache + Resilient Counter (Flutter)

## Goal
Ameliorer la robustesse mobile des notifications:
- ouverture rapide avec cache local,
- mode degrade lisible quand le reseau tombe,
- compteur non lu resilient.

## Delivered

### Local cache for notification feed
- Nouveau cache local `NotificationFeedCache` (SharedPreferences):
  - commandes recentes cachees
  - timestamp de derniere synchro
  - snapshot compteur non lu
- Fichier:
  - `lib/core/storage/notification_feed_cache.dart`

### Serialization support
- Ajout `toJson()` sur:
  - `OrderDto`
  - `OrderItemDto`
  - `PaymentMethodSnapshotDto`
- Permet la persistance locale des commandes pour le feed notifications.
- Fichier:
  - `lib/core/models/orders.dart`

### Providers wiring
- Nouveau provider:
  - `notificationFeedCacheProvider`
- Fichier:
  - `lib/core/providers.dart`

### Resilient unread counter
- `unreadNotificationsCountProvider`:
  - essaie d'abord le reseau,
  - met a jour le cache si succes,
  - retourne le snapshot local si echec reseau.
- Fichier:
  - `lib/features/notifications/notification_counters_provider.dart`

### Notifications screen fallback UX
- Chargement client:
  - hydrate d'abord depuis cache local si disponible,
  - ensuite tente reseau,
  - en cas d'echec reseau: garde affichage cache + message explicite.
- Fichier:
  - `lib/features/notifications/notifications_screen.dart`

## Validation
- `flutter analyze`
- `flutter test`

## Manual tests
1. Ouvrir `/notifications` avec reseau disponible pour remplir le cache.
2. Couper le reseau puis rouvrir `/notifications`.
3. Verifier affichage du feed cache et message degrade (cache local).
4. Verifier que le badge non lu Home/Profil reste base sur le snapshot cache.
5. Reactiver le reseau et verifier reprise normale + mise a jour compteur/cache.
