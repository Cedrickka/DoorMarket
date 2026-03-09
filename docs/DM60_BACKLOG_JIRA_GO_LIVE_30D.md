# DM60 - Backlog detaille pret Jira (Go-Live controle 30 jours)

## Epic
`EPIC DM60-GOLIVE-30D`

But:
- livrer un lancement production controle et fiable,
- reduire le risque incident sur checkout/paiement/support,
- obtenir une decision Go/No-Go factuelle.

## Tickets (API, Web, Flutter, DevOps, QA, Support)

| ID | Lot | Ticket | Estimation | Dependance |
|---|---|---|---:|---|
| DM60-API-01 | API | Normaliser erreurs API checkout/cart/search (codes + messages supportables) | 1j | - |
| DM60-API-02 | API | Ajouter correlationId sur toutes reponses erreurs + logs serveur | 1j | API-01 |
| DM60-API-03 | API | Rate limiting auth/search/checkout | 1.5j | - |
| DM60-API-04 | API | Endpoint health et readiness enrichis (DB, queue, mail, push) | 1j | - |
| DM60-API-05 | API | Reconciliation cron guardrails (anti-double-run + reporting) | 1.5j | - |
| DM60-API-06 | API | Alert payload standard pour incidents paiement | 1j | API-02 |
| DM60-WEB-01 | Web | Uniformiser retours erreurs UX (toasts + blocs explicites) | 1.5j | API-01 |
| DM60-WEB-02 | Web | Accessibilite base admin/client (focus, labels, contrastes critiques) | 2j | - |
| DM60-WEB-03 | Web | Audit UI tableaux admin (action menu compact partout) | 1.5j | - |
| DM60-WEB-04 | Web | Smoke e2e web parcours commande/paiement/reprise | 1.5j | API-01 |
| DM60-MOB-01 | Flutter | Stabilisation perf ecrans lourds (home/cart/checkout/orders) | 2j | - |
| DM60-MOB-02 | Flutter | Uniformiser erreurs utilisateur (retries + messages FR/EN) | 1.5j | API-01 |
| DM60-MOB-03 | Flutter | Smoke e2e mobile commande/paiement/reprise | 1.5j | API-01 |
| DM60-OBS-01 | Observability | Dashboard prod (latence, 5xx, paiements, jobs, queue) | 2j | API-02, API-04 |
| DM60-OBS-02 | Observability | Alerting SLO + seuils paiement (Slack/Email) | 1.5j | OBS-01, API-06 |
| DM60-OBS-03 | Observability | Runbook incidents SEV1/SEV2 lie aux alertes | 1j | OBS-02 |
| DM60-SEC-01 | Security | Revue OWASP Top 10 (auth, upload, injection, CSRF, headers) | 2j | - |
| DM60-SEC-02 | Security | Secrets management + rotation + check CI | 1.5j | - |
| DM60-OPS-01 | DevOps | Pipeline release: build + migrate + smoke + rollback plan | 2j | API-04 |
| DM60-OPS-02 | DevOps | Backup/restore teste (RTO/RPO documentes) | 1.5j | - |
| DM60-QA-01 | QA | Campagne regression complete web/mobile/api | 2j | WEB-04, MOB-03 |
| DM60-QA-02 | QA | Campagne charge baseline (p50/p95/p99 + seuils) | 2j | API-04 |
| DM60-QA-03 | QA | Test de non-regression reconciliation/commissions/notifications | 1.5j | API-05 |
| DM60-SUP-01 | Support | Simulation incidents L1/L2 (paiement, commande, adresse, notif) | 1j | OBS-03 |
| DM60-DOC-01 | Doc | Dossier Go/No-Go (gates, preuves, KPI, risques restants) | 1j | QA-01, QA-02, SUP-01 |

## Definition of Done (globale)
- Code merge + build vert.
- Tests unitaires/integration impactes passent.
- Cas manuels du ticket valides.
- Logs/metrics exploitables en production.
- Documentation ticket mise a jour.

## Plan de tests manuels (resume)

### Checkout/Paiement
- creer commande depuis web et mobile.
- simuler paiement reussi, echec, reprise.
- verifier statut commande et notification associee.

### Reconciliation/Commissions
- verifier calcul frais produit + regles dynamiques.
- verifier transitions payout et export.
- verifier absence d erreur 400 LINQ.

### Support/Operations
- injecter incident paiement (mock) et verifier alerte.
- tracer correlationId de bout en bout.
- executer playbook L1 puis escalade L2.

## Cadence execution conseillee
- Sprint S1: API-01..06 + WEB-01 + MOB-02
- Sprint S2: OBS-01..03 + OPS-01 + SUP-01
- Sprint S3: SEC-01..02 + QA-01..03
- Sprint S4: soft launch + DM60-DOC-01 + decision Go/No-Go

## Execution technique
- `DM60-WEB-04` smoke automatise ajoute:
  - fichier test: `DoorMarket.Tests/WebCheckoutSmokeTests.cs`
  - couverture:
    - pre-checkout web pret a commander,
    - creation commande,
    - paiement PayPal avec premier echec puis reprise reussie,
    - verification statut final `Paid` + panier vide,
    - verification donnees de reprise panier (`/checkout`).
  - commande:
    - `dotnet test DoorMarket.Tests --filter FullyQualifiedName~WebCheckoutSmokeTests`
- `DM60-MOB-03` smoke automatise ajoute:
  - fichier test: `DoorMarket.Flutter/doormarket_flutter/test/features/checkout/mobile_checkout_smoke_test.dart`
  - couverture:
    - panier mobile + pre-checkout,
    - creation commande checkout mobile,
    - paiement Mobile Money avec premier echec puis reprise reussie,
    - verification commande `Paid` + panier vide + statut reprise inactif.
  - commande:
    - `flutter test test/features/checkout/mobile_checkout_smoke_test.dart`
- `DM60-MOB-01` stabilisation perf ecrans lourds:
  - ecrans touches:
    - `home_screen.dart`: `PageStorageKey`, `cacheExtent`, `RepaintBoundary` cartes lourdes, cache image reduit.
    - `cart_screen.dart`: `AutomaticKeepAliveClientMixin`, `PageStorageKey`, `RepaintBoundary` items panier, cache image ajuste.
    - `checkout_screen.dart`: `AutomaticKeepAliveClientMixin`, `PageStorageKey` scroll, erreurs non bloquantes stabilisees.
    - `orders_screen.dart`: `AutomaticKeepAliveClientMixin`, `PageStorageKey`, `RepaintBoundary` cartes commandes.
- `DM60-MOB-02` uniformisation erreurs utilisateur:
  - nouveau service commun:
    - `DoorMarket.Flutter/doormarket_flutter/lib/core/services/user_error_service.dart`
  - ecrans branches:
    - `home`, `cart`, `checkout`, `orders`, `order_details`
  - comportement:
    - messages FR/EN homogenes,
    - parsing `ApiException`/`DioException`,
    - boutons retry explicites sur etats erreur critiques.
- `DM60-OBS-01` dashboard production (API):
  - nouveau service:
    - `DoorMarket.Api/Services/ProductionObservabilityDashboardService.cs`
  - nouvel endpoint:
    - `GET /api/admin/checkout-observability/dashboard`
  - couverture:
    - latence/paiement (snapshot checkout),
    - erreurs checkout + proxy 5xx (via events erreurs),
    - sante jobs (`CartRecovery`, `Reconciliation`, `MarketingAutomation`),
    - backlog queue (`AbandonedCartEvents`, `TransactionalNotificationLogs`) + incidents ouverts.
- `DM60-OBS-02` alerting SLO + seuils paiement (Email/Slack):
  - options enrichies:
    - `DoorMarket.Api/Services/CheckoutAlertingOptions.cs`
    - canaux: `NotifyByEmail`, `NotifyBySlack`, `SlackWebhookUrl`, `SlackMention`
    - seuils SLO/paiement configurables.
  - alerting:
    - `DoorMarket.Api/Services/CheckoutAlertingService.cs`
    - multi-canal email/slack + statut d'envoi dans `CheckoutAlertingRunResult`.
  - endpoint de lecture seuils/etat:
    - `GET /api/admin/checkout-observability/slo`
- `DM60-OBS-03` runbook incidents SEV1/SEV2:
  - document:
    - `docs/DM60_OBSERVABILITY_SEV1_SEV2_RUNBOOK.md`
  - contenu:
    - triage L1/L2, escalade, mapping alert codes -> severite, fermeture incident.
- `DM60-SEC-01` revue OWASP Top 10:
  - middleware headers API:
    - `DoorMarket.Api/Middlewares/SecurityHeadersMiddleware.cs`
  - validation upload centralisee:
    - `DoorMarket.Api/Security/UploadSecurityValidator.cs`
    - integree dans `ProductsController`, `ShopsController`, `ShopApplicationsController`, `MeController`
  - durcissement stockage upload:
    - `DoorMarket.Infrastructure/Storage/LocalFileStorage.cs`
  - garde-fou config prod:
    - validation startup dans `DoorMarket.Api/Program.cs`
  - document:
    - `docs/DM60_SECURITY_OWASP_TOP10_REVIEW.md`
- `DM60-SEC-02` secrets management + rotation + check CI:
  - suppression secrets en clair dans:
    - `DoorMarket.Api/appsettings.Development.json`
    - `DoorMarket.Api/appsettings.Production.json`
  - script de scan secrets:
    - `scripts/security/check-secrets.ps1`
  - workflow CI:
    - `.github/workflows/security-secrets-scan.yml`
  - runbook rotation:
    - `docs/DM60_SECRETS_ROTATION_RUNBOOK.md`
- `DM60-OPS-01` pipeline release + migration + smoke + rollback:
  - workflow release manuel:
    - `.github/workflows/release-pipeline.yml`
  - scripts ops:
    - `scripts/ops/release_pipeline_local.ps1`
    - `scripts/ops/run_migration.ps1`
    - `scripts/ops/release_smoke.ps1`
  - doc rollback/go-no-go:
    - `docs/DM60_OPS_01_RELEASE_PIPELINE_ROLLBACK.md`
- `DM60-OPS-02` backup/restore + RTO/RPO:
  - scripts SQL:
    - `scripts/ops/sqlserver_backup.sql`
    - `scripts/ops/sqlserver_restore.sql`
    - `scripts/ops/sqlserver_restore_smoke.sql`
  - doc:
    - `docs/DM60_OPS_02_BACKUP_RESTORE_RTO_RPO.md`
- `DM60-QA-01` regression complete web/mobile/api:
  - script:
    - `scripts/qa/run_dm60_regression.ps1`
  - doc:
    - `docs/DM60_QA_01_REGRESSION_COMPLETE_WEB_MOBILE_API.md`
- `DM60-QA-02` charge baseline p50/p95/p99:
  - script k6:
    - `scripts/perf/dm60_api_baseline.k6.js`
    - `scripts/perf/run_dm60_baseline.ps1`
  - workflow optionnel:
    - `.github/workflows/load-baseline.yml`
  - doc:
    - `docs/DM60_QA_02_LOAD_BASELINE_P50_P95_P99.md`
- `DM60-QA-03` non-regression recon/commissions/notifications:
  - script:
    - `scripts/qa/run_dm60_nonreg_recon_comm_notif.ps1`
  - workflow regression:
    - `.github/workflows/qa-regression.yml`
  - doc:
    - `docs/DM60_QA_03_NON_REGRESSION_RECON_COMM_NOTIF.md`
- `DM60-SUP-01` simulation incidents support L1/L2:
  - script:
    - `scripts/support/run_dm60_support_incident_drill.ps1`
  - couverture:
    - paiement + observabilite checkout (dashboard/slo/incidents + injection synthetic failures),
    - commande + adresse (guardrails pre-checkout, invalid delivery zone),
    - notifications (summary/incidents + acknowledge/reopen drill).
  - sorties:
    - `artifacts/support/dm60_sup01_incident_drill_*.json`
    - `artifacts/support/dm60_sup01_incident_drill_*.md`
  - doc:
    - `docs/DM60_SUP_01_SUPPORT_INCIDENT_SIMULATION.md`
- `DM60-DOC-01` dossier Go/No-Go:
  - doc principal:
    - `docs/DM60_DOC_01_GO_NO_GO_DOSSIER.md`
  - contenu:
    - gates G1..G8,
    - matrice preuves,
    - KPI de decision (p50/p95/p99, 5xx, conversion checkout, payment failures),
    - registre risques restants + mitigation + sign-off board.
