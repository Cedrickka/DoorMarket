# Phase 49 - Sprint 4.1 RFM Segmentation + Multi-Step Scenario Automation

Date: 2026-03-04

## Delivered

- Advanced RFM segmentation API (insights + bootstrap).
- Segment-aware campaign execution now supports RFM tag criteria.
- Scenario automation API for multi-step flows:
  - Welcome
  - Winback
  - Churn risk
- Admin web integration on `/admin/marketing`:
  - RFM insights panel
  - RFM bootstrap action
  - Scenario automation run action
  - Scenario result feedback

## New API endpoints

- `GET /api/admin/marketing/segments/rfm-insights?windowDays=180`
- `POST /api/admin/marketing/segments/rfm-bootstrap`
- `POST /api/admin/marketing/automation/run-scenarios`

## Existing API improved

- `POST /api/admin/marketing/campaigns/{id}/run`
  - now uses shared execution logic
  - includes attribution metrics for coupon-linked campaigns
  - supports richer segment criteria including RFM tags

## RFM tags used

- `champions`
- `loyal`
- `potential`
- `at_risk`
- `hibernating`
- `new`

## Technical validation executed

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` -> success
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` -> success
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminMarketingControllerTests"` -> success (9/9)

## Manual QA checklist

1. Open `/admin/marketing`.
2. In "RFM segmentation and scenario automation":
3. Click `Refresh RFM insights` and verify KPIs + tag table + user sample load.
4. Click `Bootstrap RFM segments` and verify success snackbar.
5. Click `Run scenarios` with defaults and verify success snackbar.
6. Verify campaign ROI section refreshes and shows updated runs.
7. In campaign list, confirm scenario campaigns exist (names with `Scenario ... - S1/S2/S3`).
8. Trigger `Run now` on one campaign and confirm run count/date changes.

## Notes

- Scenario progression applies step cadence guards:
  - S1: at least 24h
  - S2: after S1 and at least 48h
  - S3: after S2 and at least 72h
- MudBlazor analyzer warnings are pre-existing and non-blocking for this phase.
