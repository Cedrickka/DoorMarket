# Phase 48 - Sprint 4 Marketing Automation + Campaign ROI Dashboard

Date: 2026-03-04

## Scope delivered

- API admin marketing:
  - campaign summary KPI
  - campaign ROI list (paged)
  - campaign ROI CSV export
  - batch campaign automation runner (scheduled style)
- API campaign run:
  - manual run now computes audience + run metrics
  - attribution for coupon-linked campaigns (revenue/discount in window)
- Web admin (`/admin/marketing`):
  - new section "Campaign automation and ROI"
  - run campaign automation button
  - run-now per campaign
  - campaign summary KPI cards
  - campaign ROI table
  - CSV export action

## API endpoints added

- `GET /api/admin/marketing/campaigns/summary?from=&to=`
- `GET /api/admin/marketing/campaigns/roi?from=&to=&q=&status=&page=&pageSize=`
- `GET /api/admin/marketing/campaigns/roi/export?from=&to=&q=&status=`
- `POST /api/admin/marketing/automation/run-campaigns`

## Updated behavior

- `POST /api/admin/marketing/campaigns/{id}/run` now:
  - uses unified execution logic
  - computes `TargetUsers`, `SentCount`, `RevenueAttributed`, `DiscountCost`
  - updates `LastRunAtUtc`
  - auto moves Draft -> Active

## Validation executed

- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` -> success
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminMarketingControllerTests"` -> success (6/6)

## Manual QA checklist

1. Open `/admin/marketing`.
2. Confirm banner section still loads and edit/create/delete still works.
3. In "Campaign automation and ROI":
4. Click `Refresh campaign data` and verify campaign table + ROI table load.
5. Click `Run campaign automation` and verify success snackbar with processed/run counts.
6. Click `Run now` on one campaign and verify last run date updates after refresh.
7. Click `Export ROI CSV` and verify file download with expected columns.
8. Filter by campaign status and verify list/ROI updates.
9. Validate summary cards change when date range changes.

## Notes

- Existing MudBlazor analyzer warnings remain in project (pre-existing); no new blocking errors.
- ROI attribution currently uses paid orders tied to campaign coupon code in attribution window.
