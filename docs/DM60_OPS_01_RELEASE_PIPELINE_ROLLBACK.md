# DM60-OPS-01 - Release pipeline (build + migrate + smoke + rollback)

Date: 2026-03-06

## Artefacts implementes

- Workflow CI/CD manuel:
  - `.github/workflows/release-pipeline.yml`
- Scripts OPS:
  - `scripts/ops/release_pipeline_local.ps1`
  - `scripts/ops/run_migration.ps1`
  - `scripts/ops/release_smoke.ps1`

## Pipeline GitHub (manual dispatch)

Entrées:
- `environment`: `staging` ou `production`
- `run_migration`: `true|false`
- `run_runtime_smoke`: `true|false`

Secrets requis:
- `STAGING_DB_CONNECTION_STRING`
- `PROD_DB_CONNECTION_STRING`
- `STAGING_API_BASE_URL`
- `PROD_API_BASE_URL`

Etapes:
1. Restore + build (`Api`, `Web`, `Tests`)
2. Smoke tests code-level:
   - `WebCheckoutSmokeTests`
   - `AdminReconciliationControllerTests`
   - `AdminCommissionsControllerTests`
   - `AdminNotificationsControllerTests`
3. Migration EF (optionnelle)
4. Runtime smoke (optionnel) via endpoints prod/staging

## Execution locale (ops)

```powershell
./scripts/ops/release_pipeline_local.ps1 `
  -Environment Production `
  -ConnectionString "Server=...;Database=...;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True;" `
  -SmokeBaseUrl "https://api.door-market.com"
```

## Smoke runtime valide

Le script `release_smoke.ps1` verifie:
- `/ping`
- `/api/health/live`
- `/api/health/ready`
- `/api/products?page=1&pageSize=1`
- `/api/shops?page=1&pageSize=1`
- `/api/search/suggestions?q=te`

## Rollback plan

### Cas A: echec applicatif sans migration destructive
1. Re-deployer l’artefact precedent (API/Web).
2. Re-lancer smoke runtime.
3. Ouvrir incident + joindre correlation IDs.

### Cas B: echec post-migration
1. Mettre API en maintenance/read-only.
2. Restaurer la base depuis le dernier backup valide.
3. Re-deployer artefact precedent.
4. Re-lancer smoke runtime.
5. Verifier reconciliation + paiements + notifications.

### Cas C: echec partiel (smoke KO mais app en ligne)
1. Stopper traffic (ou route canary -> 0%).
2. Basculer vers version precedente.
3. Lancer verification post-rollback (health + commandes).

## Critere Go/No-Go release

Go si:
- build vert,
- migrations vertes,
- smoke runtime vert,
- alertes SEV1/SEV2 absentes.

No-Go si:
- migration en erreur,
- `health/ready` unhealthy,
- regression recon/commissions/notifs.
