# Phase 44 - DM30 Search & Discovery v1 Completion

## Objective
Close remaining DM30 gaps across API, Web, Flutter, QA and documentation.

## Delivered

### API
- Added robust search text normalization utility:
  - `DoorMarket.Api/Utils/SearchTextNormalizer.cs`
  - trim, whitespace normalization, accents folding, tokenization.
- Upgraded `SearchController`:
  - token-based filtering across products/shops/categories.
  - relevance ranking strengthened with explicit rating boost.
  - popular-query suggestions added in `GET /api/search/suggestions` (`Type=Query`).
- Upgraded `SearchAnalyticsController`:
  - new internal tracking endpoint: `POST /api/search/analytics/track`
  - keeps existing query/click endpoints and shared persistence logic.

### Web
- `TopBar` global search now has live suggestions dropdown.
- `/search` page now includes fuller filter set:
  - products: price min/max, rating min, shop, city, country, stock/promo, category, sort.
  - shops: category, rating min, city, country, verified/recommended, sort.
- Full search state persistence in querystring (filters/sort/pagination/tab/query) + restoration on reload.

### Flutter
- Search API/model parity with backend filters:
  - products: min/max/rating/shop/category/city/country.
  - shops: rating/category/city/country.
- Search screen:
  - live suggestions under search bar.
  - bottom-sheet based filters UX.
  - filter summary + apply workflow.

### Tests
- Added/updated API tests:
  - popular query suggestion inclusion.
  - accent-folded query matching.
  - relevance ordering with rating boost.
  - generic tracking endpoint click flow.

### QA/Perf/Docs
- Manual QA plan consolidated:
  - `docs/DM30_QA_MANUAL_PLAN.md`
- Performance baseline package:
  - `scripts/perf/search_baseline.k6.js`
  - `docs/DM30_PERF_BASELINE.md`
- User/admin/support guide:
  - `docs/DM30_USER_GUIDE_SEARCH.md`

## Validation executed
- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` (success)
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` (success; existing MudBlazor warnings remain)
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminSearchRulesControllerTests|FullyQualifiedName~SearchControllerTests|FullyQualifiedName~SearchAnalyticsControllerTests"` (success)
- `flutter analyze lib/core/widgets/dm_search_bar.dart lib/core/models/search.dart lib/core/api/search_api.dart lib/features/search/search_screen.dart` (success)

## Remaining note
- Perf baseline script is ready; execution requires a running target environment and should be run in staging/prod-like infra.
