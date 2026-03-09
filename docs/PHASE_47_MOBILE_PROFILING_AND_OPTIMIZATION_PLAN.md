# Phase 47 - Mobile Profiling (Timeline + CPU/GPU) et plan d'optimisation

Date: 2026-03-04  
Scope: Flutter Android (device: TECNO CK7n, mode profile)

## 1) Methode de profiling utilisee

- Auto-parcours instrumente: `home -> categories -> search -> shops -> cart -> notifications -> home_return`
- Mesures frame-level via `SchedulerBinding.addTimingsCallback`:
  - CPU frame time = `buildMs`
  - GPU frame time = `rasterMs`
  - Total frame time = `build + raster`
  - `% slow frames` au-dessus de 16.67 ms et 33 ms
- Timeline run:
  - Baseline: `autoprofile_report.json`
  - Apres optimisations Perf-1: `autoprofile_report_after2.json`

## 2) Resultats avant/apres (p95 + jank)

| Ecran | p95 total avant (ms) | p95 total apres (ms) | Delta | slow >16ms avant | slow >16ms apres | Delta | p95 CPU avant | p95 CPU apres | p95 GPU avant | p95 GPU apres |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| home | 142.52 | 81.44 | -61.08 | 42.86% | 37.17% | -5.69 pts | 7.83 | 22.56 | 73.21 | 56.05 |
| categories | 75.16 | 35.61 | -39.55 | 84.00% | 19.12% | -64.88 pts | 10.72 | 23.97 | 71.89 | 18.45 |
| search | 51.26 | 49.32 | -1.94 | 32.63% | 12.50% | -20.13 pts | 12.93 | 22.40 | 33.36 | 15.29 |
| shops | 18.30 | 12.50 | -5.80 | 6.32% | 1.09% | -5.23 pts | 3.64 | 1.47 | 15.70 | 10.96 |
| cart | 16.22 | 15.69 | -0.53 | 4.11% | 3.90% | -0.21 pts | 2.04 | 2.75 | 15.42 | 13.25 |
| notifications | 36.52 | 19.77 | -16.75 | 7.89% | 7.89% | 0.00 pt | 7.75 | 7.60 | 25.11 | 15.45 |
| home_return | 28.67 | 20.62 | -8.05 | 10.09% | 7.89% | -2.20 pts | 5.89 | 3.38 | 18.24 | 15.66 |

Lecture:
- Les gains viennent surtout de la baisse GPU (raster), surtout sur `categories`, `home`, `notifications`.
- `home` et `search` restent au-dessus de la cible en p95 total.
- Le p95 CPU monte sur `home/categories/search`, ce qui signale un cout build/layout encore trop eleve.

## 3) Statut global Sprint Perf

- Objectif 60 fps: **non atteint** sur `home`, `categories`, `search`.
- Ecrans proches de la cible: `shops`, `cart`.
- Ecrans a stabiliser: `notifications`, `home_return`.

## 4) Plan d'optimisation ecran par ecran (priorise)

## P0 - Home (impact conversion le plus fort)

Probleme:
- p95 total encore tres haut (81.44 ms), jank 37.17%.
- GPU et CPU eleves en meme temps.

Actions:
- Remplacer les sections verticales lourdes par `SliverList` avec delegation stricte.
- `RepaintBoundary` explicite par bloc (header, carrousel promo, categories tabs, sections produits).
- Eviter tout rebuild global quand la recherche locale change (scinder via `ValueListenableBuilder`/`Selector`).
- Limiter le prefetch image simultane (cap 2-3), deferer sections hors viewport.
- Desactiver ombres + gradients complexes sur premiere trame uniquement, activer apres premier idle frame.

Cible:
- p95 total <= 40 ms
- slow >16ms <= 15%

## P0 - Search

Probleme:
- p95 total 49.32 ms, slow >16ms encore 12.5%.
- CPU p95 22.4 ms (filtrage/rebuild), GPU deja mieux.

Actions:
- Debounce input 250-300ms + cancellation token strict.
- Result items const-constructibles + cle stable + `AutomaticKeepAliveClientMixin`.
- Precompute highlights/sorting en isolate si liste > N (ex: >120 items).
- Uniformiser gabarit des cards resultats pour limiter re-layout.

Cible:
- p95 total <= 28 ms
- CPU p95 <= 10 ms

## P1 - Categories

Probleme:
- Gros gain deja obtenu mais CPU p95 encore haut (23.97 ms).

Actions:
- Passer categories left-rail + grid produits sur un seul `CustomScrollView` avec slivers.
- Diminuer encore pageSize initial (si payload > 60) + pagination lazy.
- Memoization du mapping categorie -> produits.

Cible:
- p95 total <= 25 ms
- CPU p95 <= 12 ms

## P1 - Notifications

Probleme:
- p95 divise par 2 mais jank inchange (7.89%).

Actions:
- Stabiliser layout refresh (pas de widget flexible sans contrainte en list item).
- Skeleton simple fixed height.
- Fallback empty state sans icones animees ni ombres.

Cible:
- p95 total <= 16 ms
- slow >16ms <= 3%

## P2 - Cart / Shops / Home_return

Probleme:
- Presque conformes mais encore pics ponctuels.

Actions:
- Definir hauteur fixe des rows panier (eviter overflow + relayout cascade).
- Boutons quantite en taille fixe (pas de texte auto-size).
- Cache image miniature dedie (96x96 max) pour panier/shops.

Cible:
- p95 total <= 14 ms
- slow >16ms <= 2%

## 5) KPIs perf a suivre en CI/dev

- p95 total frame par ecran
- p95 CPU / p95 GPU par ecran
- slow frames >16.67 ms (%)
- slow frames >33 ms (%)
- Max frame (outlier watch)

Seuils gate proposes (warning, pas blocage build pour l'instant):
- Home: warn si p95 > 45 ms
- Categories/Search: warn si p95 > 30 ms
- Autres ecrans: warn si p95 > 20 ms

## 6) Commande de comparaison reutilisable

```powershell
powershell -ExecutionPolicy Bypass -File scripts/perf/compare_flutter_autoprofile.ps1 `
  -BaselinePath DoorMarket.Flutter/doormarket_flutter/autoprofile_report.json `
  -AfterPath DoorMarket.Flutter/doormarket_flutter/autoprofile_report_after2.json `
  -OutMarkdownPath DoorMarket.Flutter/doormarket_flutter/autoprofile_compare.md
```
