# DM45 - QA Plan + Support Guide (Returns / Coupons / Cart Recovery)

Date: 2026-03-02

## Scope

- Returns (API + Web + Flutter)
- Coupons (API + Web + Flutter)
- Abandoned cart recovery (API worker + notifications)

## Environments and prechecks

1. API deployed with migration `AddAbandonedCartEvents` applied.
2. SMTP configured for realistic email checks.
3. `CartRecovery` config enabled:
   - `Enabled=true`
   - `StaleAfterMinutes` >= 60 for normal run, lower for QA acceleration
   - `AntiSpamWindowMinutes` configured (example `1440`)
4. Seed data available:
   - at least 1 client with paid+delivered order (for returns/reviews checks)
   - at least 1 active coupon
   - at least 1 cart with items older than stale threshold

## QA matrix

### A) Returns

1. Client creates valid return request.
2. Duplicate open return on same order blocked.
3. Requested amount > refundable amount blocked.
4. Admin transitions:
   - `Requested -> Approved`
   - `Requested -> Rejected`
   - `Approved -> Refunded`
5. Invalid transitions return conflict and clear message.
6. History timeline visible and coherent.

### B) Coupons

1. Admin CRUD coupon lifecycle:
   - create, edit, activate/deactivate, delete.
2. Coupon validation endpoint:
   - valid coupon => `applied=true`, `discount>0`
   - invalid coupon => warning `coupon_not_applied`
3. Delivery zone validation:
   - required zone missing => blocking issue
   - valid zone => delivery fee included
4. Web checkout UX:
   - apply coupon, clear coupon, zone revalidation
5. Flutter checkout UX:
   - apply coupon, invalid coupon message, address revalidation
6. Order payload:
   - `promoCode` sent only when coupon is effectively applied

### C) Abandoned cart recovery

1. Candidate detection:
   - cart with items and stale activity is detected
2. Anti-spam:
   - second run inside anti-spam window does not create a new event
3. Conversion skip:
   - if order created after last cart activity, reminder is skipped
4. Email reminder logging:
   - `TransactionalNotificationLogs` row with `CartAbandonedReminderEmail`
5. Push base behavior:
   - if `PushEnabled=false`, no push attempt
   - if enabled without provider, push failure is logged explicitly

## Operational SQL checks

### Latest abandoned cart events

```sql
SELECT TOP 50
  Id, UserId, CartId, Currency, ItemCount, Subtotal,
  LastCartActivityAtUtc, DetectedAtUtc, ReminderStatus,
  ReminderAttemptCount, SentChannels, Error, ReminderSentAtUtc
FROM AbandonedCartEvents
ORDER BY DetectedAtUtc DESC;
```

### Abandoned cart notification logs

```sql
SELECT TOP 100
  Id, NotificationType, Channel, Recipient, Status, Error, AttemptedAtUtc
FROM TransactionalNotificationLogs
WHERE NotificationType IN ('CartAbandonedReminderEmail', 'CartAbandonedReminderPush')
ORDER BY AttemptedAtUtc DESC;
```

### Coupon audit logs

```sql
SELECT TOP 100
  Id, Source, PromoCode, Applied, Discount, Currency, CreatedAtUtc
FROM PromoAuditLogs
WHERE Source IN ('CartValidateCoupon', 'Checkout')
ORDER BY CreatedAtUtc DESC;
```

### Return requests timeline

```sql
SELECT TOP 100
  r.Id, r.OrderId, r.UserId, r.Status, r.RequestedAmount, r.ApprovedAmount,
  r.RefundedAmount, r.CreatedAtUtc, r.UpdatedAtUtc
FROM ReturnRequests r
ORDER BY r.CreatedAtUtc DESC;
```

## Support playbook

### Incident type: Return blocked

1. Validate order eligibility (paid + delivered/completed).
2. Check existing open return for same order.
3. Compare requested amount vs refundable balance.
4. If transition failure: verify current status and allowed transition graph.

### Incident type: Coupon not applied

1. Validate coupon status:
   - active
   - validity dates
   - usage/budget limits
   - scope (shop/category/country/city)
2. Validate cart context:
   - subtotal threshold
   - delivery zone requirements
3. Check `PromoAuditLogs` (`CartValidateCoupon`, `Checkout`) for exact message.

### Incident type: No abandoned cart reminder received

1. Verify stale cart conditions:
   - cart has items
   - last activity older than `StaleAfterMinutes`
2. Verify anti-spam:
   - recent event exists inside `AntiSpamWindowMinutes`
3. Verify conversion skip:
   - user placed order after last cart activity
4. Check notification logs:
   - `CartAbandonedReminderEmail` status / error
5. Check SMTP configuration and sender domain reputation.

## Support response templates

### Coupon issue

"Nous avons verifie votre code promo et son perimetre d'application. Le code n'a pas ete applique car [raison]. Nous vous invitons a [action]."

### Return issue

"Votre demande de retour est actuellement au statut [statut]. Pour l'etape suivante, veuillez [action]."

### Cart reminder issue

"Nous avons verifie la relance panier: [envoyee/non envoyee], raison: [raison]. Nous avons [action corrective]."

## DM46 extension (AB + push reel + KPI)

### Additional prechecks

1. Migration `AddCartRecoveryObservabilityAndPushDevices` appliquee.
2. `CartRecovery:AbTestEnabled` et `CartRecovery:HoldoutPercent` configures selon scenario QA.
3. Push provider credentials renseignes (`Push:Fcm` et/ou `Push:Apns`).
4. Au moins un token device enregistre via `POST /api/me/push-devices/register`.

### Additional QA checks

1. Client UX reprise panier:
   - web `/cart` et mobile cart screen affichent la banniere reprise
   - CTA ouvre le checkout
2. Admin KPI:
   - `/api/admin/cart-recovery/summary` renvoie `GroupA*`, `GroupB*`, `UpliftPct`, `JobLatencyP50Ms`, `JobLatencyP95Ms`
3. Push reel:
   - relance sur user avec token actif => tentative push visible en logs
   - token invalide => echec permanent + token desactive
4. A/B holdout:
   - groupe `B` => status `Skipped`
   - groupe `A` => envoi normal selon canaux dispo

### Additional SQL checks

```sql
SELECT TOP 100
  Id, UserId, CartId, ExperimentGroup, ReminderStatus, DetectedAtUtc, ReminderSentAtUtc
FROM AbandonedCartEvents
ORDER BY DetectedAtUtc DESC;
```

```sql
SELECT TOP 100
  Id, StartedAtUtc, EndedAtUtc, DurationMs, Success,
  CandidatesScanned, EventsCreated, RemindersSent, RemindersFailed
FROM CartRecoveryJobRuns
ORDER BY StartedAtUtc DESC;
```

```sql
SELECT TOP 100
  Id, UserId, Platform, IsActive, LastSeenAtUtc, UpdatedAtUtc
FROM UserPushDevices
ORDER BY LastSeenAtUtc DESC;
```
