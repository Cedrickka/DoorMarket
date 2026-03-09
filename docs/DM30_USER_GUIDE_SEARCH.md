# DM30 - Guide utilisateur recherche (Client / Admin / Support)

## 1) Côté client (Web + Mobile)

### Rechercher
1. Saisir une requête dans la barre de recherche.
2. Utiliser les suggestions live pour accélérer.
3. Choisir onglet: Produits, Boutiques, Catégories.

### Filtrer et trier
- Produits: prix min/max, note min, catégorie, boutique, ville, pays, stock, promo.
- Boutiques: tri, catégorie, note min, ville, pays, vérifiées, recommandées.
- Tri: pertinence, prix, note, nouveautés (selon onglet).

### Conseils
- Requête courte (2+ caractères) pour activer suggestions.
- Utiliser filtres progressivement pour éviter "0 résultat".

## 2) Côté admin

### Monitoring analytique
- Consulter `/admin/search-analytics` pour:
  - volume de recherches
  - CTR
  - no-result rate
  - top/no-result queries

### Optimisation rules
- Utiliser `/admin/search-rules` pour:
  - suggestions auto depuis no-result
  - application bulk
  - suivi efficacité
  - auto-deactivate / auto-reactivate
  - mode simulation (dry-run) + aperçu candidats

## 3) Côté support

### Diagnostic rapide ticket recherche
1. Collecter: query utilisateur, device, heure, source web/mobile.
2. Reproduire sur `/search` avec mêmes filtres.
3. Vérifier analytics (query/click/no-result).
4. Vérifier règles actives pour la query dans admin search-rules.

### Cas fréquents
- "Aucun résultat": vérifier orthographe, catégorie, pays/ville, stock/promo.
- "Résultat non pertinent": vérifier tri, filtres, puis rule mapping admin.
- "Incohérence web/mobile": comparer query params envoyés à l'API.

## 4) Glossaire
- `relevance`: tri par pertinence textuelle + boosts (promo/stock/note).
- `no-result rate`: % de recherches sans résultat.
- `CTR`: ratio clics / recherches.
