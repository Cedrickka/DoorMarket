# Phase 21 - Notification Incidents + CSV Export

## Goal
Ameliorer l'exploitation admin des notifications transactionnelles avec:
- une vue incidents non resolus (groupes d'echecs),
- un export CSV filtrable pour audit externe.

## Delivered

### API admin notifications
- Nouveau endpoint: `GET /api/admin/notifications/transactions/incidents`
  - filtres: `orderId`, `type`, `from`, `to`
  - options: `minFailures`, `take`
  - regroupe les echecs non resolus par `(orderId, type, recipient)`
  - expose `FailedCount`, `FirstAttemptUtc`, `LastAttemptUtc`, `LastError`, `LastLogId`

- Nouveau endpoint: `GET /api/admin/notifications/transactions/export.csv`
  - filtres: `orderId`, `type`, `status`, `from`, `to`
  - option: `maxRows`
  - export CSV des tentatives de notifications

- Renforcement du bulk retry:
  - traitement des cas `OrderId null` coherents avec les autres calculs unresolved.

### Web admin `/admin/notifications`
- Ajout section "Unresolved incidents":
  - table incidents avec compteur d'echecs
  - action `Retry` sur `LastLogId`
- Ajout bouton `Export CSV` avec `max rows` configurable.
- Ajout filtre `Incident min failures`.

## Files
- `DoorMarket.Api/Controllers/AdminNotificationsController.cs`
- `DoorMarket.Web/Components/Pages/Admin/Notifications.razor`
- `DoorMarket.Tests/AdminNotificationsControllerTests.cs`

## Validation
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminNotificationsControllerTests"`
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj`

## Manual tests
1. Ouvrir `/admin/notifications` et verifier la section `Unresolved incidents`.
2. Verifier qu'un incident avec plusieurs echecs est agrege (count > 1).
3. Cliquer `Retry` sur un incident et verifier la creation d'une nouvelle tentative.
4. Cliquer `Export CSV` avec filtres actifs et verifier le fichier telecharge.
5. Verifier que `maxRows` limite bien le volume exporte.
