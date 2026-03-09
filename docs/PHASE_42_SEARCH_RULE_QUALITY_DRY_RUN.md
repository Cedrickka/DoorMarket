# Phase 42 - Search Rule Quality Dry-Run (Simulation)

## Objective
Secure search-rule quality operations by introducing a simulation mode (`dry-run`) before applying real deactivation/reactivation changes.

## Backend changes

### 1) Dry-run support for quality actions
- Updated:
  - `DoorMarket.Api/Controllers/AdminSearchRulesController.cs`
- Extended endpoints:
  - `POST /api/admin/search-rules/effectiveness/deactivate-problematic`
  - `POST /api/admin/search-rules/effectiveness/reactivate-recovered`

Request additions:
- `dryRun` (bool, default `false`)

Response additions:
- deactivate action:
  - `wouldDeactivateRules`
  - `dryRun`
- reactivate action:
  - `wouldReactivateRules`
  - `dryRun`

Behavior:
- if `dryRun=true`:
  - evaluates and matches candidate rules
  - returns "would change" counters
  - performs no DB write
- if `dryRun=false`:
  - preserves existing live behavior and applies updates

### 2) DTO updates
- Updated request/response DTOs in `AdminSearchRulesController.cs` for both actions to carry `dryRun` and projected counters.

## Web admin changes

- Updated:
  - `DoorMarket.Web/Components/Pages/Admin/SearchRules.razor`
- Added in effectiveness panel:
  - toggle `Mode simulation (dry-run)` (enabled by default)
- Updated action payloads:
  - sends `dryRun` for deactivation and reactivation quality actions
- Updated snackbar feedback:
  - explicit mode (`dry-run` vs `live`)
  - displays changed count and projected count

## Tests added/updated

- Updated:
  - `DoorMarket.Tests/AdminSearchRulesControllerTests.cs`
- Added:
  - `DeactivateProblematicRules_DryRun_DoesNotChangeRule`
  - `ReactivateRecoveredRules_DryRun_DoesNotChangeRule`

Coverage intent:
- dry-run returns expected projected counters
- no persistence side effects are applied when simulation mode is active

## Validation executed

- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminSearchRulesControllerTests|FullyQualifiedName~SearchControllerTests|FullyQualifiedName~SearchAnalyticsControllerTests"` (success)

## Manual test checklist

1. Open `/admin/search-rules`.
2. In "Rule effectiveness", keep `Mode simulation (dry-run)` enabled.
3. Run `Deactivate problematic rules` with thresholds that should match at least one active rule.
4. Confirm snackbar indicates `dry-run` and non-zero `would deactivate` while active state remains unchanged in table.
5. Run `Reactivate recovered rules` with thresholds that should match at least one inactive recovered rule.
6. Confirm snackbar indicates `dry-run` and non-zero `would reactivate` while inactive state remains unchanged.
7. Disable `Mode simulation (dry-run)`.
8. Re-run one action and confirm real state change is now applied and reflected in rules table.
