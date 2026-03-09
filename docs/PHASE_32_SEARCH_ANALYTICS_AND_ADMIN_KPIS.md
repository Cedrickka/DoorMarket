# Phase 32 - Search Analytics and Admin KPIs

## Objective
Add a robust analytics foundation for search behavior across web/mobile and expose actionable admin metrics:
- Track search query events.
- Track search result click events.
- Provide admin KPIs and ranked query insights.

## Backend changes

### 1) Persistence model
- Added entity: `SearchAnalyticsEvent`
  - `DoorMarket.Domain/Entities/SearchAnalyticsEvent.cs`
- Added EF configuration:
  - `DoorMarket.Infrastructure/Persistence/Configurations/SearchAnalyticsEventConfiguration.cs`
- Added DbSet:
  - `DoorMarket.Infrastructure/Persistence/DoorMarketDbContext.cs`

Tracked fields include:
- Event typing (`Query`, `Click`)
- Query and normalized query
- Target context (`TargetType`, `TargetId`, `Position`)
- Search context (`ResultsCount`, `DurationMs`, `Page`, `Sort`, `FiltersHash`)
- Attribution (`UserId`, `SessionId`, `Source`, `CountryTag`)
- Timestamp (`OccurredAtUtc`)

### 2) Migration
- Added migration:
  - `DoorMarket.Infrastructure/Persistence/Migrations/20260302084717_AddSearchAnalyticsEvents.cs`
  - Designer + snapshot updated by EF.

### 3) Tracking API (public)
- Added controller:
  - `DoorMarket.Api/Controllers/SearchAnalyticsController.cs`
- Endpoints:
  - `POST /api/search/analytics/query`
  - `POST /api/search/analytics/click`

Input normalization includes:
- Query cleanup + lowercase normalized key
- Source normalization (`Web`, `Mobile`, `Admin`)
- Country tag uppercase
- Value clamping (page/position/duration/results)

### 4) Admin analytics API
- Added calculator service:
  - `DoorMarket.Api/Services/SearchAnalyticsCalculator.cs`
- Added event constants:
  - `DoorMarket.Api/Services/SearchAnalyticsEvents.cs`
- Registered service:
  - `DoorMarket.Api/Program.cs`
- Added admin controller:
  - `DoorMarket.Api/Controllers/AdminSearchAnalyticsController.cs`

Endpoints:
- `GET /api/admin/search-analytics/kpis`
- `GET /api/admin/search-analytics/top-queries`
- `GET /api/admin/search-analytics/no-result-queries`

Supported filters:
- `from`, `to`, `source`, `take` (where relevant)

## Tests

### Added tests
- `DoorMarket.Tests/SearchAnalyticsControllerTests.cs`
  - Query tracking persistence + normalization
  - Click tracking validation
  - End-to-end admin aggregate verification (KPI/top/no-result)

### Validation commands used
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~SearchAnalyticsControllerTests|FullyQualifiedName~SearchControllerTests|FullyQualifiedName~AdminReconciliationControllerTests|FullyQualifiedName~AdminNotificationsControllerTests"`
- `dotnet build DoorMarket.sln`

Both commands passed. Existing warnings unrelated to this phase remain in the solution.
