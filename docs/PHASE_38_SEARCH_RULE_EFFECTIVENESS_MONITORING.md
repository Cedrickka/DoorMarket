# Phase 38 - Search Rule Effectiveness Monitoring

## Objective
Track real impact of search rules after creation by exposing operational metrics:
- usage volume per trigger
- no-result rate
- click-through rate (CTR)
- performance status to prioritize cleanup

## Backend changes

### 1) Effectiveness endpoint
- Updated:
  - `DoorMarket.Api/Controllers/AdminSearchRulesController.cs`
- Added endpoint:
  - `GET /api/admin/search-rules/effectiveness`

Query params:
- `from`, `to`, `source`
- `q`, `active`
- `take`
- `problemOnly`
- `minQueries`

Per-rule metrics returned:
- `queryCount`
- `noResultCount`
- `clickCount`
- `noResultRatePct`
- `ctrPct`
- `lastSeenAtUtc`
- `performance` (`NoData`, `Good`, `Watch`, `NeedsAttention`, `Critical`)

### 2) Performance heuristics
- `Critical`: no-result rate >= 60%
- `NeedsAttention`: no-result rate >= 30%
- `Good`: no-result rate <= 10% and CTR >= 15%
- `Watch`: all other non-empty data
- `NoData`: no query events

`problemOnly=true` keeps rules with enough traffic (`minQueries`) and poor metrics.

## Web admin changes

- Updated:
  - `DoorMarket.Web/Components/Pages/Admin/SearchRules.razor`
- Added "Rule effectiveness" panel:
  - filters: date range, source, top N, min queries, problems only
  - table with status chip + metrics (`queries`, `no-result`, `CTR`, `last seen`)
  - explicit refresh action

## Tests added/updated

- Updated:
  - `DoorMarket.Tests/AdminSearchRulesControllerTests.cs`
- Added test:
  - `GetEffectiveness_WithEvents_ReturnsComputedMetrics`
  - verifies computed metrics and performance status.

## Validation executed

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` (success)
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` (success; existing warnings remain)
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminSearchRulesControllerTests|FullyQualifiedName~SearchControllerTests|FullyQualifiedName~SearchAnalyticsControllerTests"` (success)

## Manual test checklist

1. Open `/admin/search-rules`.
2. Create one or more rules and generate search traffic on their trigger queries.
3. Generate mixed outcomes:
   - some queries with `0` results
   - some queries with valid results and clicks
4. In "Rule effectiveness", set date/source filters and click `Refresh performance`.
5. Verify metrics per trigger:
   - `Queries`
   - `No-result % (count)`
   - `CTR % (count)`
   - `Performance` badge
6. Enable `Problems only` and verify low-quality rules are isolated.
7. Use these insights to adjust rules (edit/delete or new mapping).
