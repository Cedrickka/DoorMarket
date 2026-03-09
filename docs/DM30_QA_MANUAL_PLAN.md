# DM30 - Plan QA manuel consolidé (API / Web / Mobile)

## Pré-requis
- API démarrée.
- Données de test: produits multi-catégories, boutiques vérifiées/non-vérifiées, notes variables, promos actives, stock varié.
- Web + Flutter connectés à la même API.

## 1) API Search

### 1.1 Suggestions
1. `GET /api/search/suggestions?q=app&limit=8`.
2. Vérifier présence de types `Product`, `Shop`, `Category`.
3. Vérifier le ranking lexical: exact > prefix > contains.
4. Vérifier présence éventuelle de `Query` (requêtes populaires) si analytics existe.

### 1.2 Produits
1. `GET /api/search/products?q=apple&page=1&pageSize=12`.
2. Vérifier pagination (`page`, `pageSize`, `total`).
3. Tester filtres: `minPrice`, `maxPrice`, `inStockOnly`, `promotedOnly`, `ratingMin`, `shopId`, `categoryId`, `city`, `countryTag`.
4. Vérifier tri: `relevance`, `price_asc`, `price_desc`, `rating_desc`, `newest`.

### 1.3 Boutiques
1. `GET /api/search/shops?q=app&page=1&pageSize=12`.
2. Tester filtres: `verifiedOnly`, `recommended`, `ratingMin`, `categoryId`, `city`, `countryTag`.
3. Vérifier tri: `relevance`, `rating_desc`, `popular_desc`, `newest`.

### 1.4 Catégories
1. `GET /api/search/categories?q=appl&limit=24`.
2. Vérifier compteurs `activeProductsCount`, `activeShopsCount`.

### 1.5 Tracking
1. `POST /api/search/analytics/query` et `POST /api/search/analytics/click`.
2. `POST /api/search/analytics/track` avec `eventName=search_query_submitted`.
3. `POST /api/search/analytics/track` avec `eventName=search_result_clicked` + target.
4. Vérifier persistance DB et normalisation (`normalizedQuery`).

## 2) Web (/search + topbar)

1. Ouvrir `/search?q=apple`.
2. Vérifier suggestions live pendant saisie.
3. Vérifier états loading / empty / error sur les 3 onglets.
4. Ouvrir filtres produits: prix min/max, note min, catégorie, boutique, ville, pays, stock, promo.
5. Ouvrir filtres boutiques: tri, catégorie, note min, ville, pays, verified, recommended.
6. Changer tri + filtres + page et recharger navigateur: vérifier restauration via querystring.
7. Utiliser recherche topbar globale: suggestions live + navigation correcte vers `/search`.
8. Vérifier tracking analytics via actions de recherche/clic.

## 3) Flutter (SearchScreen)

1. Ouvrir écran Search.
2. Vérifier suggestions live sous la barre (tap suggestion => recherche).
3. Ouvrir bottom sheet `Filtres`.
4. Onglet Produits: appliquer filtres (prix/note/catégorie/boutique/ville/pays/stock/promo) puis vérifier résultats.
5. Onglet Boutiques: appliquer filtres (tri/catégorie/note/ville/pays/verified/recommended) puis vérifier résultats.
6. Vérifier pagination produit/boutique.
7. Vérifier tracking query/click mobile.

## 4) Non-régression rapide
- Home, Catalog Shops, Shop details, Product details toujours navigables.
- Aucun crash sur saisie vide, query très longue, filtres incohérents.
