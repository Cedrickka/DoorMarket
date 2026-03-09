# Phase 53 - Sprint 5 A/B Uplift Auto + Action Recommendations

## Scope
- Add automatic A/B uplift analysis for marketing campaign experiments.
- Surface decision-ready recommendations in admin marketing dashboard.
- Add CSV export for experiment insights and recommendations.

## API
- `GET /api/admin/marketing/campaigns/experiments/insights`
  - Computes baseline vs variant automatically from available experiment groups.
  - Returns:
    - baseline/variant metrics
    - ROI uplift, revenue-per-sent uplift, delivery delta
    - confidence level (`Low|Medium|High`)
    - winner experiment
    - action recommendations list
    - full experiment aggregates

- `GET /api/admin/marketing/campaigns/experiments/insights/export`
  - CSV export of:
    - summary uplift metrics
    - recommendations
    - experiment rows

## Web Admin
- `/admin/marketing`:
  - Section `Experiment ROI by run type` now includes:
    - button `Export A/B insights CSV`
    - block `Automatic A/B uplift analysis`
    - KPI cards (confidence, winner, ROI uplift, revenue/sent uplift, delivery delta)
    - recommendation table (priority, action, rationale, execution hint)

## QA checklist
1. Open `/admin/marketing`.
2. Refresh campaign analytics with a period that includes campaign runs.
3. Confirm section `Automatic A/B uplift analysis` is visible.
4. Verify baseline, variant, winner, and uplift metrics are populated when 2+ groups exist.
5. Verify recommendation rows are displayed.
6. Click `Export A/B insights CSV` and validate file sections:
   - `Summary`
   - `Recommendation`
   - `Experiment`

