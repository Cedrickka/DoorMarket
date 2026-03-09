# Phase 43 - Search Rule Dry-Run Candidate Preview (Web Admin)

## Objective
Improve operational clarity in admin by showing which search-rule triggers are impacted during simulation (`dry-run`) actions before applying live changes.

## Web admin changes

- Updated:
  - `DoorMarket.Web/Components/Pages/Admin/SearchRules.razor`

### 1) Dry-run preview block
- Added a dedicated preview panel in the Rule Effectiveness section.
- Shows latest simulated candidates for:
  - problematic rule deactivation
  - recovered rule reactivation
- Displays triggers as chips with color coding:
  - red = deactivation candidates
  - green = reactivation candidates
- Limit applied to keep UI readable:
  - max 20 unique triggers per action.

### 2) State management for preview
- Added local state collections:
  - `_lastDryRunDeactivateTriggers`
  - `_lastDryRunReactivateTriggers`
- Updated action handlers:
  - populate preview lists only when `dryRun=true`
  - clear corresponding preview list on live execution (`dryRun=false`).

## UX impact

- Admin operators can validate "what would change" visually, not only via counters/snackbar.
- Reduces accidental live actions and improves confidence in threshold tuning.

## Validation executed

- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` (success; existing MudBlazor analyzer warnings remain)

## Manual test checklist

1. Open `/admin/search-rules`.
2. Keep `Mode simulation (dry-run)` enabled.
3. Click `Disable problematic rules` with thresholds expected to match rules.
4. Verify deactivation candidate chips appear in preview panel.
5. Click `Reactivate recovered rules` in dry-run.
6. Verify reactivation candidate chips appear in preview panel.
7. Disable dry-run and rerun one action.
8. Verify corresponding preview list is cleared and real state change occurs.
