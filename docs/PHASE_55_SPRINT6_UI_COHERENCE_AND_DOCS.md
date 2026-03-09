# Phase 55 - Sprint 6: UI Coherence + Usage/Operations Documentation

## Goals
- Stabilize visual consistency across admin web screens.
- Introduce reusable UI layout conventions (header, shell, panel, equal-height grids).
- Produce clear user and operations documentation for daily use and production support.

## Web UI coherence delivered

### 1) Shared UI system tokens and layout helpers
- Updated: `DoorMarket.Web/wwwroot/css/doormarket.css`
- Added reusable primitives:
  - `dm-page-header`, `dm-page-title-wrap`
  - `dm-page-shell`
  - `dm-panel`, `dm-panel-soft`
  - `dm-equal-grid` (equal card height behavior in MudGrid)
  - `dm-toolbar-row`
  - `dm-section-title`, `dm-section-sub`
  - `dm-empty-state`

### 2) Applied to core admin pages
- `DoorMarket.Web/Components/Pages/Admin/Dashboard.razor`
  - page shell alignment
  - KPI and analytics sections using `dm-equal-grid`
  - panel consistency (`dm-panel`)
- `DoorMarket.Web/Components/Pages/Admin/Reconciliation.razor`
  - page header/shell alignment
  - assistant and bank/reconciliation blocks with coherent panel style
  - equal-height operational cards
- `DoorMarket.Web/Components/Pages/Admin/Marketing.razor`
  - page header/shell alignment
  - coherent shells on left/right panes and campaign analytics block
- `DoorMarket.Web/Components/Pages/Admin/Commissions.razor`
  - page header/shell alignment
  - preview panel styled with shared panel helpers

## Documentation delivered
- User guide (web + mobile): `docs/DOORMARKET_USER_GUIDE_WEB_MOBILE.md`
- Operations runbook: `docs/DOORMARKET_OPERATIONS_RUNBOOK.md`

## Build validation
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` (success)
- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` (success)
- `dotnet build DoorMarket.Tests/DoorMarket.Tests.csproj` (success)

## Manual QA checklist
1. Open `/admin/dashboard`, `/admin/reconciliation`, `/admin/marketing`, `/admin/commissions`.
2. Verify cards align in height in row grids (no visual “staggering” effect).
3. Verify top page spacing/title/subtitle is consistent.
4. Verify form/action rows preserve spacing on desktop and mobile widths.
5. Verify no regression on existing actions:
   - Dashboard refresh
   - Reconciliation payout transitions
   - Marketing filters and exports
   - Commission create/update/delete + preview
