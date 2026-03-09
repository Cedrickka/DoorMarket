# Phase 51 - Sprint 4.3 Campaign ROI Advanced Filters + Cohorts + KPI Export

Date: 2026-03-04

## Delivered

- API marketing analytics upgraded with advanced filters:
  - `segmentId`
  - `channel` (`email`, `push`, `inapp`, `multi`)
  - `minSent`
  - `minRoi`
  - existing date/status/q filters still supported
- New cohort analytics endpoint by segment.
- New KPI export endpoint (summary + cohorts + campaign rows).
- Admin web marketing page upgraded:
  - advanced ROI filter controls (campaign name, segment cohort, channel, min sent, min ROI)
  - cohort ROI table by segment
  - new `Export KPI CSV` action
  - ROI table now displays segment and channels

## API endpoints added/extended

- Extended:
  - `GET /api/admin/marketing/campaigns`
  - `GET /api/admin/marketing/campaigns/summary`
  - `GET /api/admin/marketing/campaigns/roi`
  - `GET /api/admin/marketing/campaigns/roi/export`
- Added:
  - `GET /api/admin/marketing/campaigns/cohorts`
  - `GET /api/admin/marketing/campaigns/kpi/export`

## Validation executed

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` -> success
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` -> success
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminMarketingControllerTests"` -> success (13/13)

## Manual QA checklist

1. Open `/admin/marketing`.
2. In campaign ROI section, set filters:
   - campaign name
   - segment cohort
   - channel
   - min sent / min ROI
3. Click `Refresh campaign data` and verify:
   - campaign list updates
   - ROI table updates
   - cohort table updates
4. Click `Export ROI CSV` and verify CSV includes `Segment` and `Channels` columns.
5. Click `Export KPI CSV` and verify file includes sections:
   - summary metrics
   - cohort metrics
   - campaign rows

## Notes

- Web build reports pre-existing MudBlazor analyzer warnings across many files; no new blocking compile errors introduced by this phase.
