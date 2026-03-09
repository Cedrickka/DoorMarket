# DM60-DOC-01 - Dossier Go/No-Go (Gates, preuves, KPI, risques)

## Contexte
Ce dossier consolide la decision Go/No-Go pour le lancement controle 30 jours (`EPIC DM60-GOLIVE-30D`).

Reference backlog:
- `docs/DM60_BACKLOG_JIRA_GO_LIVE_30D.md`

## Perimetre de decision
- API (`DoorMarket.Api`)
- Web (`DoorMarket.Web`)
- Mobile Flutter (`DoorMarket.Flutter/doormarket_flutter`)
- Flows critiques: catalogue -> panier -> checkout -> paiement -> commande -> notifications -> support.

## Gates Go/No-Go

| Gate | Description | Seuil | Evidence obligatoire | Statut |
|---|---|---|---|---|
| G1 Regression fonctionnelle | Web + API + Mobile smoke critiques passent | 0 test bloqueur en echec | `docs/DM60_QA_01_REGRESSION_COMPLETE_WEB_MOBILE_API.md`, TRX, rapport mobile smoke | TODO |
| G2 Performance baseline | p50/p95/p99 API sous seuil cible | p95 <= 900ms, 5xx < 1% | `docs/DM60_QA_02_LOAD_BASELINE_P50_P95_P99.md`, export k6 | TODO |
| G3 Non-regression finance/ops | Reconciliation/commissions/notifications stables | 0 regression critique | `docs/DM60_QA_03_NON_REGRESSION_RECON_COMM_NOTIF.md` | TODO |
| G4 Observabilite + alerting | Dashboard prod + alertes SLO paiements actifs | incidents visibles + alert channels testes | `docs/DM60_OBSERVABILITY_SEV1_SEV2_RUNBOOK.md`, captures dashboard | TODO |
| G5 Support readiness L1/L2 | Simulation incidents paiement/commande/adresse/notif executee | drill `PASS` ou `WARN` justifie | `docs/DM60_SUP_01_SUPPORT_INCIDENT_SIMULATION.md`, `artifacts/support/*.md` | TODO |
| G6 Securite | OWASP top 10 revue + secrets hors code | aucune fuite secretes / controles actifs | `docs/DM60_SECURITY_OWASP_TOP10_REVIEW.md`, `docs/DM60_SECRETS_ROTATION_RUNBOOK.md` | TODO |
| G7 Exploitation/rollback | pipeline release + migration + smoke + rollback documentes | dry-run complet valide | `docs/DM60_OPS_01_RELEASE_PIPELINE_ROLLBACK.md`, logs release | TODO |
| G8 Backup/restore | RTO/RPO verifies par test restoration | RTO/RPO conformes au runbook | `docs/DM60_OPS_02_BACKUP_RESTORE_RTO_RPO.md` | TODO |

Regle de decision:
- `GO` seulement si tous les gates bloquants G1..G8 sont `PASS`.
- `NO-GO` si au moins 1 gate est `FAIL`.
- `CONDITIONAL GO` autorise uniquement si gate `WARN` avec mitigation approuvee et owner/date fixes.

## Evidence pack a joindre

### 1) QA
- resultat `run_dm60_regression.ps1`
- resultat `run_dm60_nonreg_recon_comm_notif.ps1`
- evidence mobile smoke (sortie `flutter test` ou execution manuelle tracee)

### 2) Performance
- resultat `run_dm60_baseline.ps1`
- comparaison p50/p95/p99 vs seuils cibles

### 3) Support drill
- resultat `run_dm60_support_incident_drill.ps1`
- rapport JSON + MD le plus recent

### 4) Ops/Sec
- logs de release pipeline + migration + smoke
- preuve backup/restore
- preuve scan secrets CI + revue OWASP

## Commandes standard

```powershell
# QA regression
powershell -ExecutionPolicy Bypass -File scripts/qa/run_dm60_regression.ps1 -Configuration Release

# QA non-regression finance/notifications
powershell -ExecutionPolicy Bypass -File scripts/qa/run_dm60_nonreg_recon_comm_notif.ps1 -Configuration Release

# Perf baseline API
powershell -ExecutionPolicy Bypass -File scripts/perf/run_dm60_baseline.ps1 -BaseUrl "https://api.door-market.com"

# Support drill L1/L2
powershell -ExecutionPolicy Bypass -File scripts/support/run_dm60_support_incident_drill.ps1 \
  -BaseUrl "https://api.door-market.com" \
  -AdminBearerToken "<ADMIN_JWT>" \
  -ClientBearerToken "<CLIENT_JWT>" \
  -InjectCheckoutFailureEvents \
  -AcknowledgeAndReopenFirstIncident \
  -Strict
```

## KPI de decision (a renseigner)

| KPI | Cible | Valeur mesuree | Source | Statut |
|---|---:|---:|---|---|
| API latency p50 (ms) | <= 300 | TODO | k6 baseline | TODO |
| API latency p95 (ms) | <= 900 | TODO | k6 baseline | TODO |
| API latency p99 (ms) | <= 1500 | TODO | k6 baseline | TODO |
| HTTP 5xx rate (%) | < 1.0 | TODO | observability dashboard | TODO |
| Checkout submit -> paid (%) | >= 75 | TODO | checkout observability | TODO |
| Payment technical failure (%) | <= 5 | TODO | checkout observability | TODO |
| Incident drill overall status | PASS/WARN | TODO | support drill report | TODO |

## Risques restants et mitigation

| Risque | Impact | Probabilite | Mitigation | Owner | ETA |
|---|---|---|---|---|---|
| Mobile Money provider instable en pic | Eleve | Moyen | fallback provider + alerting immediat + retry policy | Payments | TODO |
| Saturation queue notifications | Moyen | Moyen | threshold queue + auto-scale worker + DLQ audit | Platform | TODO |
| Regressions UI mobile sur devices low-end | Moyen | Moyen | smoke mobile quotidien + profilage ecrans lourds | Mobile | TODO |
| Derive perf DB en charge reelle | Eleve | Faible/Moyen | index tuning + load test hebdo + capacity guardrails | Backend | TODO |

## Decision board (signature)

- Date decision (UTC): `TODO`
- Environnement: `Staging` / `Production` (cocher)
- Decision finale: `GO` / `CONDITIONAL GO` / `NO-GO`
- Conditions si `CONDITIONAL GO`:
  - `TODO`
- Signataires:
  - Product: `TODO`
  - Tech Lead: `TODO`
  - Ops/DevOps: `TODO`
  - Support Lead: `TODO`

## Historique
- 2026-03-06: dossier initialise (structure gates + preuves + KPI + risques).
