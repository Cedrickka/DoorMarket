# Phase 34 - Admin Search Analytics Dashboard (Web)

## Objective
Add a dedicated admin web UI to exploit search analytics data for product/catalog decisions.

## Delivered

### 1) New admin page
- Route: `/admin/search-analytics`
- File: `DoorMarket.Web/Components/Pages/Admin/SearchAnalytics.razor`

Main capabilities:
- Date filters (`from`, `to`)
- Source filter (`All`, `Web`, `Mobile`, `Admin`)
- KPI cards:
  - Searches
  - Clicks
  - CTR
  - No-result searches
  - No-result rate
  - Unique queries/users/sessions
  - Average latency
- Top queries table:
  - searches, clicks, CTR, no-result count, latency, last seen
  - action button to test query in storefront search
- No-result queries table:
  - count, last seen
  - action button to analyze directly in storefront search
- Priority warning block:
  - highlights high-volume queries with high no-result ratio

### 2) Admin navigation integration
- Added menu entry under admin section:
  - `DoorMarket.Web/Components/Layout/NavMenu.razor`
  - Label bilingual runtime:
    - `Search Analytics` / `Analytique recherche`

### 3) Admin dashboard shortcut
- Added quick button in main admin dashboard:
  - `DoorMarket.Web/Components/Pages/Admin/Dashboard.razor`
  - Navigates to `/admin/search-analytics`

### 4) Styling
- Added small CSS block for alert title/list:
  - `DoorMarket.Web/wwwroot/css/doormarket.css`

## Validation
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj`: success.
- Existing MudBlazor analyzer warnings remain (pre-existing in project).

## Manual test checklist
1. Login as admin and open `/admin/search-analytics`.
2. Verify KPI cards load and react to date filters.
3. Switch source filter (`Web` then `Mobile`) and verify KPI/table values change.
4. Use action button on a top query and verify redirect to `/search?q=...`.
5. Use action button on a no-result query and verify redirect to `/search?q=...`.
6. Confirm sidebar menu contains “Search Analytics / Analytique recherche”.
7. From `/admin/dashboard`, click “Search analytics” shortcut and verify navigation.
