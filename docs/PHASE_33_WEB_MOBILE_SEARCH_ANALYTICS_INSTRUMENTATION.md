# Phase 33 - Web and Mobile Search Analytics Instrumentation

## Objective
Connect real client events (Web + Flutter mobile) to search analytics endpoints introduced in phase 32.

## Web (Blazor) changes

File updated:
- `DoorMarket.Web/Components/Pages/Catalog/Search.razor`

Added:
- Session analytics id persistence in browser session storage (`dm_search_analytics_session_id`).
- Automatic query tracking (`POST /api/search/analytics/query`) after successful search result loads:
  - products tab
  - shops tab
  - categories tab
- Click tracking (`POST /api/search/analytics/click`) for:
  - search suggestion click
  - product result click
  - shop result click
- Captured metadata:
  - query, result count, latency, page, sort
  - filter signature
  - source (`Web`)
  - session id
  - country tag when available

## Flutter mobile changes

Files updated:
- `DoorMarket.Flutter/doormarket_flutter/lib/core/models/search.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/core/api/search_api.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/features/search/search_screen.dart`

Added:
- Analytics payload models:
  - `SearchAnalyticsQueryEvent`
  - `SearchAnalyticsClickEvent`
- API methods:
  - `trackQuery(...)`
  - `trackClick(...)`
- Search screen instrumentation:
  - Persistent session id in shared preferences (`dm_search_analytics_session_id`)
  - Query tracking after each successful search load with latency and result count
  - Click tracking for product/shop/category result actions
  - Source marked as `Mobile`

## Validation performed

- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` (success; existing warnings remain).
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~SearchAnalyticsControllerTests|FullyQualifiedName~SearchControllerTests"` (success).
- `dart format` on modified Flutter files (success).
- `flutter analyze lib/core/models/search.dart lib/core/api/search_api.dart lib/features/search/search_screen.dart` (no issues).

## Manual verification checklist

1. Open Web search page, search a term, and confirm a row appears in `SearchAnalyticsEvents` with `EventType = Query` and `Source = Web`.
2. Click a product result on Web search and confirm a `Click` row with `TargetType = Product`.
3. Click a shop result on Web search and confirm a `Click` row with `TargetType = Shop`.
4. Use an autocomplete suggestion and confirm a `Click` row for the suggestion target type.
5. In Flutter search screen, run a query and confirm a `Query` row with `Source = Mobile`.
6. Click a product/shop/category result in Flutter and confirm corresponding `Click` rows.
7. Open admin endpoints:
   - `GET /api/admin/search-analytics/kpis`
   - `GET /api/admin/search-analytics/top-queries`
   - `GET /api/admin/search-analytics/no-result-queries`
   and confirm values increase after manual interactions.
