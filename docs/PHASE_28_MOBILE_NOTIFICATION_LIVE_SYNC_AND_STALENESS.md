# Phase 28 - Mobile Notification Live Sync + Staleness Guard (Flutter)

## Goal
Ameliorer la fiabilite percue des notifications mobile:
- rafraichissement automatique periodique,
- indicateur explicite de fraicheur des donnees,
- action manuelle de resynchronisation immediate.

## Delivered

### Shared refresh tick
- Nouveau provider `notificationRefreshTickProvider` (tick toutes les 45 secondes).
- `unreadNotificationsCountProvider` depend maintenant de ce tick pour se recalculer regulierement.

### Notifications screen auto-sync
- L'ecran `Notifications` (admin et client) ecoute le tick et relance automatiquement le chargement.
- Nouveau bloc visuel `sync status`:
  - indique `Last sync`
  - affiche un etat stale si synchro trop ancienne (>= 2 minutes)
  - bouton `Refresh` immediat

### Timestamps
- Horodatage de derniere synchro memorise separément:
  - client: `_clientLastLoadedAt`
  - admin: `_adminLastLoadedAt`

## Files
- `DoorMarket.Flutter/doormarket_flutter/lib/features/notifications/notification_counters_provider.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/notifications/notifications_screen.dart`

## Validation
- `flutter analyze`
- `flutter test`

## Manual tests
1. Ouvrir `/notifications` et verifier l'affichage du bloc de synchro.
2. Attendre ~45 secondes et verifier que les donnees se rafraichissent automatiquement.
3. Cliquer `Refresh` et verifier mise a jour immediate + timestamp.
4. Simuler un ecran inactif (laisser ouvert longtemps), verifier message stale.
5. Revenir sur Home/Profil et verifier que les badges non lus suivent les mises a jour periodiques.
