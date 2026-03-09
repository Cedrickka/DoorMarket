# Phase 37 - Search Suggestions Bulk Apply (Admin)

## Objective
Accelerate no-result remediation by allowing admins to apply multiple high-quality suggestions in one action, with explicit quality guards.

## Backend changes

### 1) Bulk apply endpoint
- Updated:
  - `DoorMarket.Api/Controllers/AdminSearchRulesController.cs`
- Added endpoint:
  - `POST /api/admin/search-rules/suggestions/apply`

Request payload:
- `from`, `to`, `source`, `take`
- `minScore`
- `minNoResultCount`
- `confidenceFilter` (`Any`, `High`, `MediumOrHigh`)
- `isActive`
- `notePrefix`

Response payload:
- `totalSuggestions`
- `eligibleSuggestions`
- `created`
- `skippedExisting`
- `skippedInvalid`
- `skippedMissingTarget`

### 2) Safety rules enforced
- Trigger query normalization and deduplication.
- Target validation (`Product` / `Shop` / `Category` must exist).
- Score/hit/confidence filtering before creation.
- No duplicate active rule creation for existing triggers.

## Web admin changes

- Updated:
  - `DoorMarket.Web/Components/Pages/Admin/SearchRules.razor`
- Added in suggestions panel:
  - quality filters (`Min score`, `Min hits`, `Confidence`)
  - bulk action button `Apply filtered suggestions`
  - success feedback with created/skipped counters

## Tests added/updated

- Updated:
  - `DoorMarket.Tests/AdminSearchRulesControllerTests.cs`
- Added test:
  - `ApplySuggestions_WithQualityFilters_CreatesRule`
  - verifies bulk apply creates one valid search rule from repeated no-result query typo.

## Validation executed

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` (success)
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` (success; existing project warnings remain)
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminSearchRulesControllerTests|FullyQualifiedName~SearchControllerTests|FullyQualifiedName~SearchAnalyticsControllerTests"` (success)

## Manual test checklist

1. Open `/admin/search-rules`.
2. In storefront, generate repeated no-result typo queries (example: `blenderr` at least 2 times).
3. In suggestions panel:
   - set `From` / `To`
   - set `Source` (`Web` or `Mobile`)
   - click `Refresh suggestions`
4. Set bulk quality filters:
   - `Min score` (example `82`)
   - `Min hits` (example `2`)
   - `Confidence` (`Medium + High`)
5. Click `Apply filtered suggestions`.
6. Confirm snackbar counters indicate created/skipped counts.
7. Verify new rules are present in rules table and search quality is improved for the typo queries.
