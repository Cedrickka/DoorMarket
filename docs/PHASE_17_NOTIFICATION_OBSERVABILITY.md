# Phase 17 - Notification Observability (Admin)

## Goal
Rendre les notifications transactionnelles auditables et pilotables par l'admin: qui a recu quoi, quand, et avec quel statut (envoye/echec).

## Delivered

### Data model
- Nouvelle table: `TransactionalNotificationLogs`
  - `OrderId` (nullable)
  - `NotificationType`
  - `Channel`
  - `Recipient`
  - `Subject`
  - `Status` (`Sent` / `Failed`)
  - `Error` (nullable)
  - `AttemptedAtUtc`

### Logging in notification flows
- Les notifications suivantes ecrivent maintenant une trace:
  - `ClientOrderCreated`
  - `ClientPaymentPaid`
  - `ClientPaymentFailed`
  - `ShopOrderPaidPending`
  - `AdminOrderPaid`
- En cas d'echec SMTP, un log `Failed` est conserve avec le message d'erreur.

### Admin APIs
- `GET /api/admin/notifications/transactions`
  - filtres: `orderId`, `type`, `status`, `from`, `to`
  - pagination: `page`, `pageSize`
- `GET /api/admin/notifications/transactions/summary`
  - agregation par `NotificationType` (total, sent, failed, last attempt)

## Files
- `DoorMarket.Domain/Entities/TransactionalNotificationLog.cs`
- `DoorMarket.Infrastructure/Persistence/Configurations/TransactionalNotificationLogConfiguration.cs`
- `DoorMarket.Infrastructure/Persistence/DoorMarketDbContext.cs`
- `DoorMarket.Api/Services/NotificationEvents.cs`
- `DoorMarket.Api/Services/ClientOrderNotificationService.cs`
- `DoorMarket.Api/Services/OrderNotificationService.cs`
- `DoorMarket.Api/Services/AdminOrderNotificationService.cs`
- `DoorMarket.Api/Controllers/AdminNotificationsController.cs`
- `DoorMarket.Tests/AdminNotificationsControllerTests.cs`

## Validation
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminNotificationsControllerTests|FullyQualifiedName~ClientOrderNotificationServiceTests|FullyQualifiedName~OrderPaymentWorkflowServiceTests"`

## Manual tests
1. Creer une commande et verifier qu'une ligne `ClientOrderCreated` apparait dans `/api/admin/notifications/transactions`.
2. Finaliser un paiement et verifier les lignes `ClientPaymentPaid`, `ShopOrderPaidPending`, `AdminOrderPaid`.
3. Simuler un echec de paiement et verifier `ClientPaymentFailed` avec `status=Failed` ou `Sent` selon issue SMTP.
4. Tester filtres `orderId` + `status=Failed`.
5. Verifier `/summary` pour confirmer les compteurs par type.
