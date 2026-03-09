# Phase 46 - Cart Recovery AB, Push Reel, KPI

Date: 2026-03-02

## Objectif

Finaliser la relance panier abandonne avec:
- UX client web/mobile explicite pour reprendre le checkout
- suivi admin des relances et conversion
- provider push reel (FCM/APNS) avec retry/logs
- observabilite A/B et KPI (uplift, latence job, erreurs canal)

## Tickets livres

### DM46-CRT-03 (Web + Mobile UX)

- API:
  - endpoint `GET /api/cart/recovery-status`
  - DTO `CartRecoveryStatusDto` avec:
    - `hasActiveReminder`
    - `message`
    - `reminderStatus`
    - `experimentGroup`
    - `itemCount`, `subtotal`, `currency`
    - `checkoutPath`
- Web:
  - banniere "Reprendre votre panier" sur `/cart`
  - bouton "Reprendre checkout" -> `/checkout`
- Flutter:
  - chargement `recovery-status` dans `CartProvider`
  - banniere reprise panier dans `cart_screen`
  - action explicite "Resume checkout"

### DM46-CRT-04 (Admin API + Dashboard)

- API admin:
  - `POST /api/admin/cart-recovery/run-now`
  - `GET /api/admin/cart-recovery/summary`
  - `GET /api/admin/cart-recovery/events`
- Web admin:
  - nouvelle page `/admin/cart-recovery`
  - filtres date/status/group + pagination events
  - cartes KPI:
    - detectes, envoyes, failed, skipped
    - taux reprise, taux conversion
    - conversion groupe A/B + uplift
    - latence job p50/p95
    - erreurs SMTP/push
  - integration lien depuis menu admin + dashboard

### DM46-CRT-05 (Push reel)

- infra push:
  - remplacement du noop sender par `CartReminderPushSender`
  - provider `FcmPushProvider` et `ApnsPushProvider`
  - retry basique (2 tentatives) sur erreurs transitoires
  - desactivation auto des tokens en echec permanent
- API utilisateur:
  - `GET /api/me/push-devices`
  - `POST /api/me/push-devices/register`
  - `DELETE /api/me/push-devices/unregister?token=...`
- payload push:
  - type `cart_recovery`
  - deep link `/checkout`

### DM46-OBS-02 (A/B + KPI)

- data model:
  - `AbandonedCartEvent.ExperimentGroup` (`A`/`B`)
  - table `CartRecoveryJobRuns`
  - table `UserPushDevices`
- A/B:
  - holdout configurable via `CartRecovery:HoldoutPercent`
  - groupe `B` -> reminder `Skipped`
- KPI:
  - uplift conversion `A - B`
  - latence job p50/p95 depuis `CartRecoveryJobRuns`
  - erreurs SMTP/push depuis `TransactionalNotificationLogs`

## Migration EF

- `20260302145316_AddCartRecoveryObservabilityAndPushDevices`
  - ajoute `ExperimentGroup` (default `A`) sur `AbandonedCartEvents`
  - cree `CartRecoveryJobRuns`
  - cree `UserPushDevices`
  - indexes analytics/perf sur tables relance

## Configuration

`DoorMarket.Api/appsettings*.json`:
- `CartRecovery:ConversionWindowHours`
- `CartRecovery:AbTestEnabled`
- `CartRecovery:HoldoutPercent`
- `Push:Fcm:ServerKey`
- `Push:Apns:Endpoint`
- `Push:Apns:BearerToken`
- `Push:Apns:Topic`

Pre-requis Flutter push reel:
- Android: ajouter `google-services.json` dans `android/app/` et configurer Firebase project/package.
- iOS: ajouter `GoogleService-Info.plist` dans `ios/Runner/` et activer Push Notifications + Background Modes dans le provisioning.
- App mobile: autoriser les notifications au premier lancement.

## Validation executee

- `dotnet build DoorMarket.sln -v minimal /m:1`: success
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj -v minimal`: success (`101` tests)
- `flutter analyze` (`DoorMarket.Flutter/doormarket_flutter`): success

## Tests manuels recommandes

1. Web client:
   - ouvrir `/cart` avec un event relance recent
   - verifier banniere "Reprendre votre panier" + message + CTA
   - cliquer CTA et verifier redirection `/checkout`
2. Mobile Flutter client:
   - ouvrir ecran panier avec event relance actif
   - verifier banniere + resume (items/sous-total)
   - cliquer "Resume checkout" et verifier navigation checkout
3. Admin dashboard:
   - ouvrir `/admin/cart-recovery`
   - verifier chargement summary/events sans erreur
   - verifier filtres `from/to/status/group`
4. Run-now:
   - appeler `POST /api/admin/cart-recovery/run-now`
   - verifier incrementation `CartRecoveryJobRuns`
5. A/B:
   - activer `AbTestEnabled=true`, `HoldoutPercent=50`
   - lancer plusieurs runs
   - verifier coexistence events `A` et `B`
   - verifier `B` en `Skipped`
6. Push reel:
   - enregistrer token via `POST /api/me/push-devices/register`
   - forcer relance panier
   - verifier log push (`Sent` ou `Failed`) dans `TransactionalNotificationLogs`
7. KPI observabilite:
   - verifier cartes `uplift`, `p50`, `p95`, `smtp/push failures`
   - verifier coherence avec donnees SQL
