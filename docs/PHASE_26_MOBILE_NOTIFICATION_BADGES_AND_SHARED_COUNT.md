# Phase 26 - Mobile Notification Badges + Shared Unread Count (Flutter)

## Goal
Rendre les alertes non lues visibles partout dans l'application mobile,
sans attendre l'ouverture de l'ecran Notifications.

## Delivered

### Shared notification feed logic
- Nouveau module partage:
  - `ClientNotificationKind`
  - resolution kind depuis `OrderDto`
  - generation `eventId` stable pour lu/non-lu
- Fichier:
  - `lib/features/notifications/notification_feed_logic.dart`

### Shared unread counter provider
- Nouveau provider Riverpod:
  - `unreadNotificationsCountProvider`
  - calcule les non lus a partir des commandes + preferences + read state
- Fichier:
  - `lib/features/notifications/notification_counters_provider.dart`

### Home UI
- Badge non lu ajoute sur l'icone notifications du header.
- Affichage `99+` si volume eleve.
- Fichier:
  - `lib/features/home/home_screen.dart`

### Profile UI
- Badge non lu ajoute a l'entree menu `Notifications`.
- Affichage `99+` si volume eleve.
- Fichier:
  - `lib/features/profile/profile_screen.dart`

### Notifications screen refactor
- Ecran `Notifications` aligne sur la logique feed partagee
  (kind/eventId centralises).
- Fichier:
  - `lib/features/notifications/notifications_screen.dart`

## Validation
- `flutter analyze`
- `flutter test`

## Manual tests
1. Se connecter avec un compte client ayant des notifications non lues.
2. Verifier badge sur Home (icone cloche) et sur Profil > Notifications.
3. Ouvrir `/notifications`, marquer des items lus (action item ou `mark all read`).
4. Revenir sur Home/Profil et verifier mise a jour immediate des badges.
5. Verifier le format `99+` en cas de volume important.
6. Desactiver les notifications dans Settings et verifier badge a zero.
