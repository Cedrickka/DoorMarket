# Phase 20 - Operations Snapshot: Notification Alerting

## Goal
Rendre les incidents notifications visibles directement dans le pilotage operations, afin d'agir avant impact client/shop.

## Delivered

### API (`/api/admin/operations/snapshot`)
- Nouvelles metriques ajoutees:
  - `NotificationAttemptsInWindow`
  - `NotificationFailedInWindow`
  - `NotificationFailedUnresolved`
- `NotificationFailedUnresolved` est calcule sur les echecs non resolus:
  - un log `Failed` est considere resolu s'il existe un `Sent` plus recent pour meme `orderId + type + recipient`.

### Web admin dashboard
- Bloc "Operations Snapshot" enrichi avec:
  - `Notification failures (24h)`
  - `Unresolved notification failures`
- Alerte visible quand il y a des echecs non resolus, avec CTA:
  - `Open notifications` -> `/admin/notifications`

## Files
- `DoorMarket.Api/Controllers/AdminOperationsController.cs`
- `DoorMarket.Tests/AdminOperationsControllerTests.cs`
- `DoorMarket.Web/Components/Pages/Admin/Dashboard.razor`

## Validation
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminOperationsControllerTests"`
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj`

## Manual tests
1. Ouvrir `/admin/dashboard` et verifier les deux nouvelles metriques notifications dans Operations Snapshot.
2. Verifier l'affichage de l'alerte lorsque `Unresolved notification failures > 0`.
3. Cliquer `Open notifications` et confirmer la navigation vers `/admin/notifications`.
4. Rejouer des notifications failed/sent et verifier la mise a jour des compteurs apres refresh.
