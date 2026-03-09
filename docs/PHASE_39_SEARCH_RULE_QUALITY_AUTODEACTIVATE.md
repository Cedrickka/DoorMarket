# Phase 39 - Search Rule Quality Auto-Deactivation

## Objective
Add an operational safety action to quickly disable low-quality search rules based on real usage metrics.

## Backend changes

### 1) Reusable effectiveness computation
- Updated:
  - `DoorMarket.Api/Controllers/AdminSearchRulesController.cs`
- Refactored effectiveness logic into internal reusable method:
  - computes queries, no-result rate, CTR and status per rule.

### 2) New quality action endpoint
- Added endpoint:
  - `POST /api/admin/search-rules/effectiveness/deactivate-problematic`

Request parameters:
- `from`, `to`, `source`, `q`, `active`
- `take`
- `minQueries`
- `minNoResultRatePct`
- `maxCtrPct`
- `noteSuffix`

Response:
- `evaluatedRules`
- `matchedProblematicRules`
- `deactivatedRules`
- `skippedAlreadyInactive`
- thresholds used
- list of affected triggers

Behavior:
- evaluates rule quality on selected time window
- deactivates rules matching bad thresholds
- appends an optional note for traceability

## Web admin changes

- Updated:
  - `DoorMarket.Web/Components/Pages/Admin/SearchRules.razor`
- Added in "Rule effectiveness" panel:
  - thresholds inputs (`Min no-result %`, `Max CTR %`, `Min queries action`)
  - button `Disable problematic rules`
  - success feedback with deactivated/matched/evaluated counters

## Tests added/updated

- Updated:
  - `DoorMarket.Tests/AdminSearchRulesControllerTests.cs`
- Added:
  - `DeactivateProblematicRules_WithLowQualityRule_DeactivatesRule`
  - verifies a poor rule is auto-disabled and note is appended.

## Validation executed

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` (success)
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` (success; existing warnings remain)
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminSearchRulesControllerTests|FullyQualifiedName~SearchControllerTests|FullyQualifiedName~SearchAnalyticsControllerTests"` (success)

## Manual test checklist

1. Open `/admin/search-rules`.
2. Ensure there are active rules with sufficient traffic.
3. Generate bad search behavior for a rule trigger:
   - many queries
   - high no-result
   - low/no clicks
4. In "Rule effectiveness", set filters and refresh.
5. Set action thresholds:
   - `Min no-result %` (example `40`)
   - `Max CTR %` (example `8`)
   - `Min queries action` (example `5`)
6. Click `Disable problematic rules`.
7. Verify:
   - success snackbar counters
   - targeted rules become inactive in rules table
   - note contains auto-disable trace.
