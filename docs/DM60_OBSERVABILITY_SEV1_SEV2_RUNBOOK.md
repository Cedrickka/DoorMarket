# DM60 - Runbook Observabilite Checkout (SEV1 / SEV2)

## Portee
Runbook operationnel pour incidents checkout/paiement detectes par:
- `CheckoutAlertingWorker` (email/slack),
- endpoints admin observabilite (`/api/admin/checkout-observability/*`).

## Severites
- `SEV1`: conversion checkout fortement impactee (paiement indisponible ou chute majeure).
- `SEV2`: degradation partielle (latence, provider degrade, queue en backlog anormal).

## Signaux a surveiller
- Endpoint dashboard prod:
  - `GET /api/admin/checkout-observability/dashboard?defaultHours=24`
  - sections: `Checkout`, `Errors`, `Jobs`, `Queue`.
- Endpoint SLO:
  - `GET /api/admin/checkout-observability/slo?defaultHours=4`
  - compare metriques courantes vs seuils configures.
- Incidents ouverts:
  - `GET /api/admin/checkout-observability/incidents?includeResolved=false`

## Mapping alerte -> severite
- `critical_payment_success_drop`: `SEV1`
- `critical_provider_failure_*`: `SEV1`
- `warning_payment_success_drop`: `SEV2`
- `warning_payment_latency_p95`: `SEV2`
- `warning_redirect_failures`: `SEV2`
- `warning_checkout_drop_submit_to_order`: `SEV2`
- `warning_provider_failure_*`: `SEV2`

## Triage L1 (0-15 min)
1. Verifier impact dans `dashboard`:
   - `Checkout.PaymentSuccessRate`, `Checkout.LatencyP95Ms`
   - `Errors.ServerErrorProxyCount`
   - `Queue.QueueBacklogApprox`
2. Verifier providers impactes:
   - `Checkout.Providers` (success/failure/latence).
3. Ouvrir incidents actifs:
   - identifier `AlertCode`, `Provider`, `ObservedValue`, `ThresholdValue`.
4. Acquitter incident en cours de traitement:
   - `POST /api/admin/checkout-observability/incidents/{id}/acknowledge`
   - note obligatoire: owner + hypothese.

## Mitigation L2 (15-45 min)
1. Incident provider paiement:
   - basculer priorite provider stable (si routing dispo),
   - reduire retries client agressifs (eviter surcharge),
   - activer communication status interne.
2. Incident latence:
   - verifier jobs longs (`Jobs.*.DurationP95Ms`),
   - reduire charge non critique (automations lourdes).
3. Incident backlog queue:
   - verifier `PendingAbandonedCarts`, `FailedNotifications`,
   - relancer worker concerne et corriger erreurs de config canal.

## Escalade
- `SEV1`: escalade immediate on-call backend + ops + produit.
- `SEV2`: escalade si > 30 min sans amelioration.

## Resolution et fermeture
1. Verifier disparition signal sur 2 fenetres consecutives (ex: 2 x 15 min).
2. Reouvrir incident si recidive:
   - `POST /api/admin/checkout-observability/incidents/{id}/reopen`
3. Cloture:
   - documenter cause racine,
   - ajouter action preventive.

## Configuration alerting (email/slack + seuils)
Section `CheckoutAlerting`:
- `NotifyByEmail`, `AlertEmails`
- `NotifyBySlack`, `SlackWebhookUrl`, `SlackMention`
- seuils SLO/paiement:
  - `PaymentSuccessCriticalThresholdPct`
  - `PaymentSuccessWarningThresholdPct`
  - `PaymentLatencyP95WarningMs`
  - `RedirectFailuresWarningCount`
  - `ProviderFailureCriticalThresholdPct`
  - `ProviderFailureWarningThresholdPct`

## Verification manuelle rapide
1. Generer incidents (test) via donnees checkout degradees.
2. Confirmer reception email/slack.
3. Confirmer presence dans `incidents` + `dashboard` + `slo`.
4. Tester `acknowledge` puis `reopen`.
