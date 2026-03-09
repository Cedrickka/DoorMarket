# Phase 52 - Sprint 5 Experiment Insights (Marketing)

## Scope
- Add experiment-oriented analytics on campaign runs (Manual / Scheduled / Scenario).
- Expose CSV export for experiment analytics.
- Integrate experiment insights into admin marketing dashboard.

## API
- `GET /api/admin/marketing/campaigns/experiments`
  - Filters: `from`, `to`, `q`, `status`, `segmentId`, `channel`, `minSent`, `minRoi`
  - Returns aggregated metrics by experiment bucket:
    - `Experiment`, `CampaignsCount`, `RunsCount`, `TargetUsers`, `SentCount`, `FailedCount`
    - `RevenueAttributed`, `DiscountCost`, `RoiPercent`
    - `DeliveryRatePercent`, `AvgRevenuePerSent`
    - `FirstRunAtUtc`, `LastRunAtUtc`

- `GET /api/admin/marketing/campaigns/experiments/export`
  - Same filters as above.
  - Returns CSV export of experiment aggregates.

## Web Admin
- `/admin/marketing` includes a new section:
  - `Experiment ROI by run type`
  - Table with experiment-level metrics and last run timestamp.
  - Button: `Export experiments CSV`.

## QA manual checklist
1. Open `/admin/marketing`.
2. In `Campaign automation and ROI`, set filters and click `Refresh campaign data`.
3. Verify `Experiment ROI by run type` table loads rows (Manual/Scheduled/Scenario).
4. Click `Export experiments CSV` and verify file contains header:
   - `Experiment,CampaignsCount,RunsCount,...`
5. Validate period filters affect experiment table and export output.

