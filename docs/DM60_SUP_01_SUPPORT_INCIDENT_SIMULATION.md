# DM60-SUP-01 - Simulation incidents Support L1/L2

## Objectif
Executer une simulation operationnelle des incidents critiques support sur 4 flux:
- paiement,
- commande,
- adresse,
- notifications,
pour verifier que L1 et L2 disposent des signaux, actions et preuves necessaires avant Go-Live.

## Artefacts techniques
- Script principal:
  - `scripts/support/run_dm60_support_incident_drill.ps1`
- Sorties generees:
  - `artifacts/support/dm60_sup01_incident_drill_YYYYMMDD_HHMMSS.json`
  - `artifacts/support/dm60_sup01_incident_drill_YYYYMMDD_HHMMSS.md`

## Prerequis
- API accessible (`BaseUrl`).
- Token admin (optionnel mais recommande) pour observabilite et notifications.
- Token client (optionnel mais recommande) pour commande/adresse.
- Endpoints DM60 OBS actifs:
  - `/api/admin/checkout-observability/*`
  - `/api/admin/notifications/transactions/*`

## Commandes

### 1) Drill minimal (connectivite + structure)
```powershell
powershell -ExecutionPolicy Bypass -File scripts/support/run_dm60_support_incident_drill.ps1 \
  -BaseUrl "https://api.door-market.com"
```

### 2) Drill complet L1/L2 (recommande pre Go/No-Go)
```powershell
powershell -ExecutionPolicy Bypass -File scripts/support/run_dm60_support_incident_drill.ps1 \
  -BaseUrl "https://api.door-market.com" \
  -AdminBearerToken "<ADMIN_JWT>" \
  -ClientBearerToken "<CLIENT_JWT>" \
  -InjectCheckoutFailureEvents \
  -FailureEventCount 24 \
  -AcknowledgeAndReopenFirstIncident \
  -Strict
```

## Scenarios verifies

### A) Paiement + observabilite
- lit dashboard/slo/incidents checkout (avant/apres),
- injecte des evenements synthetiques `checkout_payment_initiated` et `checkout_payment_failed`,
- verifie la disponibilite des endpoints d'observabilite.

### B) Commande + adresse
- probe commande avec ID invalide (reponse controlee),
- probe `pre-checkout` pour forcer un guardrail (`delivery_zone_required` ou `empty_cart`),
- probe creation adresse avec `deliveryZoneId` invalide (erreur 400 attendue).

### C) Notifications
- lit summary + incidents transactionnels,
- simule handoff L1->L2 via `acknowledge` puis `reopen` du premier incident (si present).

## Regles de verdict
- `PASS`: checks attendus valides.
- `WARN`: signal partiel ou contexte insuffisant (ex: aucun incident a ack/reopen).
- `FAIL`: endpoint critique indisponible ou comportement non controle.
- `SKIPPED`: pre-requis non fournis (token, injection desactivee).

Le script calcule un `overallStatus` global:
- `FAIL` si un scenario est `FAIL`.
- `WARN` si aucun `FAIL` mais au moins un `WARN`.
- `PASS` si au moins un `PASS` et aucun `FAIL/WARN`.
- `SKIPPED` si tout est ignore.

## Utilisation support L1/L2
- L1 execute le drill quotidien (ou avant release), attache le `.md` au ticket incident readiness.
- L2 execute le drill complet avant fenetre de lancement et documente les anomalies.
- Chaque drill doit etre lie a un ticket Jira `DM60-SUP-01` avec:
  - date/heure UTC,
  - environment,
  - `overallStatus`,
  - actions correctives eventuelles.

## Critere de cloture DM60-SUP-01
- Au moins 1 execution `PASS` ou `WARN` justifiee en environnement cible (staging/prod).
- Aucune erreur `FAIL` non traitee sur les endpoints critiques.
- Rapport JSON + MD archives dans `artifacts/support` et lies au dossier Go/No-Go.
