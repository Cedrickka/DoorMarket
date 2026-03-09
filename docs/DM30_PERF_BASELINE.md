# DM30 - Baseline Performance Search

## Objectif
Mesurer une baseline p50/p95 des endpoints Search pour DM30.

## Script
- `scripts/perf/search_baseline.k6.js`

## Endpoints couverts
- `/api/search/suggestions`
- `/api/search/products`
- `/api/search/shops`
- `/api/search/categories`

## Exécution
1. Démarrer l'API localement (exemple `http://localhost:5000`).
2. Lancer:
```bash
k6 run -e BASE_URL=http://localhost:5000 scripts/perf/search_baseline.k6.js
```

## Seuils configurés
- `http_req_failed < 2%`
- `http_req_duration p50 < 350ms`
- `http_req_duration p95 < 900ms`

## Rapport à capturer
- Date/heure campagne
- Environnement (CPU/RAM, DB, seed)
- Résultats p50/p95/p99
- Taux d'erreurs
- Top endpoints lents + actions d'optimisation

## Statut d'exécution
- Script prêt.
- Campagne non exécutée dans ce commit (nécessite API démarrée + environnement de charge).
