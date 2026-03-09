# Phase 40 - Search Rule Effectiveness CSV Export

## Objective
Provide a clean export path for search rule performance data so product/ops teams can analyze rule quality outside the dashboard.

## Backend changes

### 1) CSV export endpoint
- Updated:
  - `DoorMarket.Api/Controllers/AdminSearchRulesController.cs`
- Added endpoint:
  - `GET /api/admin/search-rules/effectiveness/export.csv`

Supported filters:
- `from`, `to`, `source`
- `q`, `active`
- `take`
- `problemOnly`
- `minQueries`

Output columns:
- `Id`
- `TriggerQuery`
- `CanonicalQuery`
- `TargetType`
- `TargetId`
- `IsActive`
- `Performance`
- `QueryCount`
- `NoResultCount`
- `ClickCount`
- `NoResultRatePct`
- `CtrPct`
- `LastSeenAtUtc`

### 2) Reuse existing metrics engine
- CSV export uses the same effectiveness computation already used by dashboard and quality actions to keep one source of truth.

## Web admin changes

- Updated:
  - `DoorMarket.Web/Components/Pages/Admin/SearchRules.razor`
- Added "Export CSV" action in effectiveness panel:
  - uses current filters from UI
  - downloads CSV file directly in browser (`doorMarket.downloadText`)

## Tests added/updated

- Updated:
  - `DoorMarket.Tests/AdminSearchRulesControllerTests.cs`
- Added test:
  - `ExportEffectivenessCsv_WithData_ReturnsCsvFile`
  - validates content type, header line, and expected rule row values.

## Validation executed

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` (success)
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` (success; existing warnings remain)
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminSearchRulesControllerTests|FullyQualifiedName~SearchControllerTests|FullyQualifiedName~SearchAnalyticsControllerTests"` (success)

## Manual test checklist

1. Open `/admin/search-rules`.
2. In "Rule effectiveness", set filters (date/source/problemOnly/min queries).
3. Click `Export CSV`.
4. Confirm browser downloads a file named like `search-rules-effectiveness-YYYYMMDD-HHMMSS.csv`.
5. Open the file and verify:
   - header columns are present
   - rows match current dashboard filters
   - metrics align with UI values.
