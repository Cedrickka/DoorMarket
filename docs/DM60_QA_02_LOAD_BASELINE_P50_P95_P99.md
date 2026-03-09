# DM60-QA-02 - Campagne charge baseline (p50/p95/p99 + seuils)

Date: 2026-03-06

## Scripts

- `scripts/perf/dm60_api_baseline.k6.js`
- `scripts/perf/run_dm60_baseline.ps1`

## Seuils baseline proposes

- `http_req_failed < 2%`
- `p50 < 350ms`
- `p95 < 900ms`
- `p99 < 1500ms`

## Lancement

```powershell
./scripts/perf/run_dm60_baseline.ps1 -BaseUrl "https://api.door-market.com"
```

Sorties:
- JSON: `artifacts/perf/dm60_api_baseline_<timestamp>.json`
- log texte: `artifacts/perf/dm60_api_baseline_<timestamp>.txt`

## Endpoints charges

- `/ping`
- `/api/health/live`
- `/api/products`
- `/api/shops`
- `/api/search/suggestions`
- `/api/search/products`

## Tableau de restitution

| Date UTC | Env | RPS moyen | p50 | p95 | p99 | erreur % | Verdict |
|---|---|---:|---:|---:|---:|---:|---|
| YYYY-MM-DD HH:mm | prod/staging | | | | | | PASS/FAIL |

## Decision

- PASS: tous seuils respectes.
- FAIL: au moins un seuil depasse.
  - ouvrir action corrective (profiling endpoint + SQL plan + cache).
