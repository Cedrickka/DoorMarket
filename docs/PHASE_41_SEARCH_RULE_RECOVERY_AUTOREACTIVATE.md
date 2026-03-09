# Phase 41 - Search Rule Recovery Auto-Reactivation

## Objective
Complete rule lifecycle management by allowing controlled reactivation of previously disabled rules when search performance has recovered.

## Backend changes

### 1) Recovery action endpoint
- Updated:
  - `DoorMarket.Api/Controllers/AdminSearchRulesController.cs`
- Added endpoint:
  - `POST /api/admin/search-rules/effectiveness/reactivate-recovered`

Request parameters:
- `from`, `to`, `source`, `q`, `active`
- `take`
- `minQueries`
- `maxNoResultRatePct`
- `minCtrPct`
- `noteSuffix`

Response:
- `evaluatedRules`
- `matchedRecoveredRules`
- `reactivatedRules`
- `skippedAlreadyActive`
- thresholds used
- list of reactivated triggers

Behavior:
- evaluates current effectiveness per rule
- identifies recovered low-risk rules
- reactivates matching inactive rules
- appends trace note if provided

### 2) Shared effectiveness engine
- Reactivation reuses the same effectiveness computation as dashboard, CSV export and quality deactivation to keep metrics consistent.

## Web admin changes

- Updated:
  - `DoorMarket.Web/Components/Pages/Admin/SearchRules.razor`
- Added in effectiveness panel:
  - thresholds for recovery (`Max no-result %`, `Min CTR %`, `Min queries reactivate`)
  - action button `Reactivate recovered rules`
  - result feedback via snackbar counters

## Tests added/updated

- Updated:
  - `DoorMarket.Tests/AdminSearchRulesControllerTests.cs`
- Added:
  - `ReactivateRecoveredRules_WithRecoveredInactiveRule_ReactivatesRule`
  - verifies inactive rule is reactivated when thresholds are met.

## Validation executed

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` (success)
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` (success; existing warnings remain)
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminSearchRulesControllerTests|FullyQualifiedName~SearchControllerTests|FullyQualifiedName~SearchAnalyticsControllerTests"` (success)

## Manual test checklist

1. Open `/admin/search-rules`.
2. Ensure at least one inactive rule has enough good traffic (low no-result, solid CTR).
3. In "Rule effectiveness", set recovery thresholds:
   - `Max no-result %` (example `15`)
   - `Min CTR %` (example `12`)
   - `Min queries reactivate` (example `5`)
4. Click `Reactivate recovered rules`.
5. Verify snackbar counters (matched/reactivated/evaluated).
6. Confirm reactivated rules appear as active in the rules table with trace note update.
