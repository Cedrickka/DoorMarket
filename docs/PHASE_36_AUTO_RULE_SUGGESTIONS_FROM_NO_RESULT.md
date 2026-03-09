# Phase 36 - Auto Rule Suggestions from No-Result Queries

## Objective
Reduce repeated no-result searches by proposing ready-to-apply search rules in admin:
- detect frequent no-result queries
- suggest best product/shop/category target
- allow one-click rule creation from admin UI

## Backend changes

### 1) Suggestion service
- Added `SearchRuleSuggestionService`:
  - `DoorMarket.Api/Services/SearchRuleSuggestionService.cs`
- Logic:
  - reads `SearchAnalyticsEvents` for `EventType=Query` and `ResultsCount<=0`
  - aggregates by normalized query (count + last seen)
  - excludes existing `SearchQueryRules` triggers
  - matches against active products, shops, categories
  - scores candidates (exact/prefix/contains/token overlap/typo distance)
  - returns ranked suggestions with confidence

### 2) Admin API endpoint
- Updated:
  - `DoorMarket.Api/Controllers/AdminSearchRulesController.cs`
- Added endpoint:
  - `GET /api/admin/search-rules/suggestions?from=&to=&source=&take=`

### 3) Dependency injection
- Updated:
  - `DoorMarket.Api/Program.cs`
- Registration:
  - `builder.Services.AddScoped<SearchRuleSuggestionService>();`

## Web admin changes

### Search rules page enhancements
- Updated:
  - `DoorMarket.Web/Components/Pages/Admin/SearchRules.razor`
- New capabilities:
  - suggestions panel above rules list
  - filters: date range, source, top N
  - confidence/score/reason display
  - `Apply` action creates a rule directly from suggestion
  - auto-refresh rules + suggestions after apply

## Tests added/updated

- Updated:
  - `DoorMarket.Tests/AdminSearchRulesControllerTests.cs`
- Additions:
  - fixture now injects `SearchRuleSuggestionService`
  - new test `GetSuggestions_WithNoResultTypo_ReturnsSuggestedProductRule`

## Validation executed

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` (success)
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` (success; existing warnings remain)
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminSearchRulesControllerTests|FullyQualifiedName~SearchControllerTests|FullyQualifiedName~SearchAnalyticsControllerTests"` (success)

## Manual test checklist

1. Open admin page `/admin/search-rules`.
2. In storefront, execute a typo query with no results several times (example: `blenderr`).
3. Return to `/admin/search-rules`, in suggestions panel set:
   - `From`: today - 14 days
   - `To`: today
   - `Source`: `Web`
   - click `Refresh suggestions`
4. Verify a suggestion appears with:
   - trigger query
   - proposed target (type + label)
   - confidence and score
5. Click `Apply`.
6. Verify the new rule appears in the rules table.
7. Search again with the same typo query and confirm results are now improved via rule mapping.
