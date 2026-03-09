# Phase 50 - Sprint 4.2 Marketing Orchestration Scheduler + Scenario Anti-Spam

Date: 2026-03-04

## Delivered

- Scheduled marketing automation worker (hourly configurable):
  - banner automation
  - campaign batch automation
  - scenario automation
- New `MarketingAutomation` configuration section in API settings.
- Scenario anti-spam guardrails upgraded:
  - step cooldown windows configurable (S1/S2/S3)
  - per-scenario per-user cap in sliding window
  - per-user scenario touch logs persisted via `TransactionalNotificationLogs`
- Scenario automation response now exposes anti-spam metrics.

## API behavior updates

- `POST /api/admin/marketing/automation/run-scenarios` now supports optional guardrail inputs:
  - `antiSpamStep1Hours`
  - `antiSpamStep2Hours`
  - `antiSpamStep3Hours`
  - `scenarioWindowDays`
  - `scenarioMaxTouchesPerUser`
- Scenario run result now includes:
  - `antiSpamSkippedUsers` (global run count)
  - per step `antiSpamSkipped`

## Infra / runtime updates

- New service options class:
  - `DoorMarket.Api/Services/MarketingAutomationOptions.cs`
- New hosted service:
  - `DoorMarket.Api/Services/MarketingAutomationWorker.cs`
- API startup wiring:
  - `Program.cs` now registers `MarketingAutomationOptions` + `MarketingAutomationWorker`
- Config sections added:
  - `appsettings.json`
  - `appsettings.Development.json`
  - `appsettings.Production.json`

## Technical validation executed

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` -> success
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` -> success
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminMarketingControllerTests"` -> success (10/10)

## Manual QA checklist

1. Start API and verify no startup exception with `MarketingAutomation` section present.
2. In admin marketing UI, run scenarios with defaults and confirm runs are generated.
3. Re-run scenarios immediately with strict anti-spam:
4. Set `scenarioMaxTouchesPerUser=1` and cooldowns to small values.
5. Confirm response returns `antiSpamSkippedUsers > 0`.
6. Validate `stepsExecuted[*].antiSpamSkipped` is populated for later steps.
7. Check `TransactionalNotificationLogs` entries with `notificationType` like `marketing_scenario:*`.
8. Verify worker logs periodically report campaign/scenario execution metrics.

## Notes

- Guardrails are now effective both against persisted historical sends and in-memory sends produced during the same scheduler run.
- Existing campaign run metrics (target/sent/revenue/cost/ROI) remain backward compatible.
