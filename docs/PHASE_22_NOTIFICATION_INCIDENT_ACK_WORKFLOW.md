# Phase 22 - Notification Incident Acknowledge Workflow

## Goal
Rendre le pilotage des incidents notifications plus clair pour les admins:
- distinguer les incidents ouverts vs pris en charge,
- permettre un cycle explicite `Acknowledge` / `Reopen`,
- filtrer la vue pour se concentrer sur les incidents actifs.

## Delivered

### API admin notifications
- Nouveau stockage des acknowledgements:
  - table `NotificationIncidentAcknowledgements`
  - clé logique: `(OrderId, NotificationType, Recipient)`
  - état actif/inactif + métadonnées `AcknowledgedBy`, `AcknowledgedAtUtc`, `ReopenedBy`, `ReopenedAtUtc`, `Note`

- Nouveau endpoint:
  - `POST /api/admin/notifications/transactions/incidents/{lastLogId}/acknowledge`
  - body optionnel: `{ "note": "..." }`

- Nouveau endpoint:
  - `POST /api/admin/notifications/transactions/incidents/{lastLogId}/reopen`

- Extension endpoint incidents:
  - `GET /api/admin/notifications/transactions/incidents`
  - nouveau paramètre `includeAcknowledged` (bool, défaut: `false`)
  - chaque incident expose:
    - `Acknowledged`
    - `AcknowledgedAtUtc`
    - `AcknowledgedBy`
    - `AcknowledgementNote`

### Web admin `/admin/notifications`
- Nouveau filtre:
  - `Show acknowledged incidents`
- Section incidents enrichie:
  - colonne `State` (Open / Acknowledged by ... at ...)
  - action `Acknowledge` pour incident ouvert
  - action `Reopen` pour incident acknowledged

## Files
- `DoorMarket.Domain/Entities/NotificationIncidentAcknowledgement.cs`
- `DoorMarket.Infrastructure/Persistence/Configurations/NotificationIncidentAcknowledgementConfiguration.cs`
- `DoorMarket.Infrastructure/Persistence/DoorMarketDbContext.cs`
- `DoorMarket.Infrastructure/Persistence/Migrations/20260301213800_AddNotificationIncidentAcknowledgements.cs`
- `DoorMarket.Api/Controllers/AdminNotificationsController.cs`
- `DoorMarket.Web/Components/Pages/Admin/Notifications.razor`
- `DoorMarket.Tests/AdminNotificationsControllerTests.cs`

## Validation
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminNotificationsControllerTests"`
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj`

## Manual tests
1. Ouvrir `/admin/notifications`, vérifier que les incidents non ack apparaissent avec `State=Open`.
2. Cliquer `Acknowledge` sur un incident, puis vérifier qu'il disparaît si `Show acknowledged incidents` est désactivé.
3. Activer `Show acknowledged incidents` et vérifier l’affichage de l’état ack (user/date).
4. Cliquer `Reopen` sur un incident acknowledged et vérifier son retour dans la liste par défaut.
5. Vérifier que `Retry` reste disponible après `Reopen`.
