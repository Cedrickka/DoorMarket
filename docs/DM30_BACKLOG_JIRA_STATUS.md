# DM30 - Backlog Jira Status (Final)

Epic: `DM30-SEARCH-DISCOVERY-V1`

## Statut final tickets

| ID | Statut | Notes |
|---|---|---|
| DM30-API-01 | Done | Modèle `SearchQuery` en place. |
| DM30-API-02 | Done | Suggestions produits/boutiques/catégories + requêtes populaires (`Type=Query`). |
| DM30-API-03 | Done | `GET /api/search/products` paginé + filtres complets. |
| DM30-API-04 | Done | `GET /api/search/shops` paginé + filtres complets. |
| DM30-API-05 | Done | Ranking v1: exact/prefix/contains + boosts promo/stock/note. |
| DM30-API-06 | Done | Normalisation texte (trim, espaces, casse, accents, tokens). |
| DM30-API-07 | Done | Journalisation événements + endpoint interne `POST /api/search/analytics/track`. |
| DM30-API-08 | Done | Tests API renforcés; baseline perf outillée (script + seuils). |
| DM30-WEB-01 | Done | Barre recherche globale + suggestions live topbar. |
| DM30-WEB-02 | Done | Page `/search` avec états loading/empty/error. |
| DM30-WEB-03 | Done | Panneau filtres étendu (prix, stock, promo, note, catégorie, boutique, localisation). |
| DM30-WEB-04 | Done | Tri + persistance complète querystring (filtres/tri/pages/tab/query). |
| DM30-WEB-05 | Done | Tracking UI web en place. |
| DM30-MOB-01 | Done | Écran recherche + suggestions live. |
| DM30-MOB-02 | Done | Résultats + filtres complets via bottom sheet + tri. |
| DM30-MOB-03 | Done | Tracking UI mobile en place. |
| DM30-QA-01 | Done | Plan manuel consolidé: `docs/DM30_QA_MANUAL_PLAN.md`. |
| DM30-QA-02 | Done* | Outillage baseline livré (`scripts/perf/search_baseline.k6.js`), exécution à lancer sur environnement cible. |
| DM30-DOC-01 | Done | Guide utilisateur/admin/support: `docs/DM30_USER_GUIDE_SEARCH.md`. |

## Références
- Completion phase: `docs/PHASE_44_DM30_SEARCH_DISCOVERY_COMPLETION.md`
- Perf baseline: `docs/DM30_PERF_BASELINE.md`
