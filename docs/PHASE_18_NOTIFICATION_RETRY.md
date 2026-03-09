# Phase 18 - Notification Retry (Admin Recovery)

## Goal
Permettre a l'administration de rejouer rapidement les notifications transactionnelles en echec, sans intervention technique en base.

## Delivered

### Replay service
- Nouveau service: `INotificationReplayService` / `NotificationReplayService`
  - recharge un log de notification
  - verifie qu'il est en statut `Failed`
  - rejoue le flux correspondant selon `NotificationType`:
    - `ClientOrderCreated`
    - `ClientPaymentPaid`
    - `ClientPaymentFailed`
    - `ShopOrderPaidPending`
    - `AdminOrderPaid`
  - retourne un resultat explicite (`Triggered`, `IgnoredNotFailed`, `UnsupportedType`, etc.)

### API admin recovery
- `POST /api/admin/notifications/transactions/{id}/retry`
  - rejoue une notification en echec par `logId`
- `POST /api/admin/notifications/transactions/retry-failed?limit=25&type=...`
  - rejoue en batch les echecs encore non resolus
  - ignore les echecs deja resolves par un envoi `Sent` plus recent (meme order/type/recipient)

### DI
- Service enregistre dans `Program.cs`
  - `AddScoped<INotificationReplayService, NotificationReplayService>()`

## Files
- `DoorMarket.Api/Services/NotificationReplayService.cs`
- `DoorMarket.Api/Controllers/AdminNotificationsController.cs`
- `DoorMarket.Api/Program.cs`
- `DoorMarket.Tests/NotificationReplayServiceTests.cs`
- `DoorMarket.Tests/AdminNotificationsControllerTests.cs`

## Validation
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~NotificationReplayServiceTests|FullyQualifiedName~AdminNotificationsControllerTests"`

## Manual tests
1. Provoquer un echec SMTP (ou utiliser un log `Failed` existant) et recuperer son `id`.
2. Appeler `POST /api/admin/notifications/transactions/{id}/retry` et verifier retour `Triggered`.
3. Verifier qu'une nouvelle tentative apparait dans `GET /api/admin/notifications/transactions`.
4. Appeler `POST /api/admin/notifications/transactions/retry-failed?limit=20`.
5. Verifier que le batch ne rejoue pas les echecs deja resolus (`Sent` plus recent).
