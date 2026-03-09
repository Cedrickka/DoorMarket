# Phase 35 - Search Rules and No-Result Optimization

## Objective
Introduce an operational mechanism to improve unclear/no-result searches:
- synonyms (query rewrite)
- direct mapping from query to product/shop/category
- admin management UI for rules

## Backend changes

### 1) New domain model and persistence
- Added `SearchQueryRule` entity:
  - `DoorMarket.Domain/Entities/SearchQueryRule.cs`
- Added EF configuration:
  - `DoorMarket.Infrastructure/Persistence/Configurations/SearchQueryRuleConfiguration.cs`
- Added `DbSet`:
  - `DoorMarket.Infrastructure/Persistence/DoorMarketDbContext.cs`
- Migration:
  - `DoorMarket.Infrastructure/Persistence/Migrations/20260302091706_AddSearchQueryRules.cs`

### 2) Search API behavior upgrades
- Updated `SearchController`:
  - resolves active rule by `TriggerQuery`
  - applies `CanonicalQuery` rewrite when provided
  - supports direct target mapping (`TargetType` + `TargetId`) without text rewrite
  - boosts mapped target in relevance ordering
  - prioritizes mapped suggestions in autocomplete

Files:
- `DoorMarket.Api/Controllers/SearchController.cs`

### 3) Admin API for rules
- New controller:
  - `DoorMarket.Api/Controllers/AdminSearchRulesController.cs`
- Endpoints:
  - `GET /api/admin/search-rules`
  - `POST /api/admin/search-rules`
  - `PUT /api/admin/search-rules/{id}`
  - `DELETE /api/admin/search-rules/{id}`
- Validation:
  - unique `TriggerQuery`
  - requires either `CanonicalQuery` or valid target mapping
  - validates target existence

## Web admin changes

### 1) Search rules management page
- New page:
  - route: `/admin/search-rules`
  - file: `DoorMarket.Web/Components/Pages/Admin/SearchRules.razor`
- Features:
  - create/edit/delete rules
  - active/inactive status
  - search/filter list

### 2) Navigation updates
- Added admin nav entry for search rules:
  - `DoorMarket.Web/Components/Layout/NavMenu.razor`
- Added shortcut from search analytics page to rules management:
  - `DoorMarket.Web/Components/Pages/Admin/SearchAnalytics.razor`

## Tests added/updated

- Updated:
  - `DoorMarket.Tests/SearchControllerTests.cs`
  - new tests for canonical rewrite and direct category/shop mapping behavior
- Added:
  - `DoorMarket.Tests/AdminSearchRulesControllerTests.cs`
  - create/validation/update scenarios

## Validation executed

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` (success)
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` (success; existing project warnings remain)
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~SearchControllerTests|FullyQualifiedName~AdminSearchRulesControllerTests|FullyQualifiedName~SearchAnalyticsControllerTests"` (success)

## Manual test checklist

1. Apply migration and run API.
2. In admin, open `/admin/search-rules`.
3. Create a synonym rule:
   - Trigger: `mixeur`
   - Canonical: `blender`
4. Search storefront `/search?q=mixeur` and verify blender products are returned.
5. Create a direct target rule:
   - Trigger: `electro kin`
   - TargetType: `Category`
   - TargetId: `<existing category guid>`
6. Search `/search?q=electro%20kin` and verify mapped category products appear.
7. In `/admin/search-analytics`, confirm problematic no-result queries can be opened and then covered by new rules.
