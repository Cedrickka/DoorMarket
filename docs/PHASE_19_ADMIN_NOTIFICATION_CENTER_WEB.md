# Phase 19 - Admin Notification Center (Web)

## Goal
Donner aux administrateurs une interface web claire pour suivre les notifications transactionnelles et lancer les retries sans passer par des appels API manuels.

## Delivered

### New admin page
- Nouvelle page Blazor: `/admin/notifications`
  - Filtres:
    - `from` / `to`
    - `type`
    - `status`
    - `orderId`
    - `pageSize`
  - Résumé par type (total/sent/failed/dernier attempt)
  - Liste paginée des transactions
  - Retry unitaire pour les lignes `Failed`
  - Bulk retry des échecs non résolus (`limit`, optionnellement filtré par type)

### Navigation
- Entrée ajoutée dans le menu admin:
  - `Administration -> Notifications`

## Files
- `DoorMarket.Web/Components/Pages/Admin/Notifications.razor`
- `DoorMarket.Web/Components/Layout/NavMenu.razor`

## Validation
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj`
  - build OK (warnings existants hors scope déjà présents dans le projet)

## Manual tests
1. Ouvrir `/admin/notifications` avec un compte admin.
2. Vérifier le chargement du résumé et de la table de transactions.
3. Appliquer des filtres (date/type/status/orderId) et vérifier les résultats.
4. Cliquer `Retry` sur une ligne `Failed` et vérifier le snackbar + refresh.
5. Cliquer `Retry unresolved failed` et vérifier le résumé `Candidates/Triggered/Ignored/Failed`.
6. Confirmer qu’une notification déjà résolue (échec puis succès plus récent) n’est pas rejouée en bulk.
