# Phase 54 - Sprint 5: Dynamic Commissions + Assisted Reconciliation

## Scope delivered
- Dynamic commission engine with rule hierarchy:
  - `Global` -> `Shop` -> `Category` -> `Product` (most specific wins)
  - Priority fallback (`Priority` asc)
  - Active window (`StartsAtUtc` / `EndsAtUtc`)
  - Optional `Currency`, optional min/max unit price
- Checkout commission computation now applies active dynamic rules before product default commission.
- Assisted reconciliation insights (admin API + dashboard panel) to detect payout anomalies and recommend actions.
- New admin page for dynamic commission management and rule fee preview.

## Database
- New table: `CommissionRules`
  - fields: `Name`, `ScopeType`, scope IDs (`ScopeShopId`, `ScopeCategoryId`, `ScopeProductId`),
    fee mode/value, windows, `Priority`, `IsActive`, metadata.
- Migration:
  - `20260304141344_AddDynamicCommissionRules`

## API

### Dynamic commissions
- `GET /api/admin/commissions/rules`
  - filters: `active`, `scopeType`, `q`
- `POST /api/admin/commissions/rules`
  - create a dynamic commission rule
- `PUT /api/admin/commissions/rules/{id}`
  - update an existing rule
- `DELETE /api/admin/commissions/rules/{id}`
  - remove a rule
- `GET /api/admin/commissions/rules/resolve-preview?productId=...&unitPrice=...`
  - preview applied commission (dynamic rule or product default)

### Assisted reconciliation
- `GET /api/admin/reconciliation/assistant`
  - filters: `from`, `to`, `shopId`
  - optional SLA params: `draftSlaDays`, `approvedSlaDays`
  - returns:
    - summary counters
    - insight list with severity, detail, recommended action, affected payout IDs

## Web admin
- New page: `/admin/commissions`
  - list/filter rules
  - create/update/delete rule
  - fee preview by product + unit price
- Navigation:
  - new admin menu entry: `Dynamic Commissions`
- Reconciliation dashboard (`/admin/reconciliation`)
  - new section: `Reconciliation assistant`
  - anomaly table with severity chips and recommended actions

## Tests added
- `AdminCommissionsControllerTests`
  - invalid scope payload validation
  - preview resolves dynamic product rule
- `OrderServicePromoTests`
  - dynamic global rule overrides product default
  - product-scoped rule wins over global rule
- `AdminReconciliationControllerTests`
  - assistant detects stale draft, partial paid, missing reference

## Manual QA checklist
1. Run migration:
   - `dotnet ef database update --project DoorMarket.Infrastructure --startup-project DoorMarket.Api`
2. Open `/admin/commissions`:
   - create a `Global` percent rule (USD, active)
   - create a `Product` flat rule for a product
   - verify product rule precedence over global in preview block
3. Place a checkout on that product:
   - confirm `OrderItems.PlatformFeeAtPurchase` reflects dynamic rule
4. Open `/admin/reconciliation`:
   - verify `Reconciliation assistant` panel loads
   - confirm anomaly rows appear when stale/partial/missing-reference payouts exist
5. Verify existing payout workflow still works:
   - Draft -> Approved -> Paid -> Reversed
