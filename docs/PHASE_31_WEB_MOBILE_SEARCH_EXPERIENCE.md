# Phase 31 - Web + Mobile Search Experience

Date: 2026-03-02

## Objectif

Brancher une recherche unifiee (produits, boutiques, categories) dans les interfaces web et mobile, avec une experience claire et actionnable.

## Modifications web

- Top bar web:
  - recherche utilisable au clavier (`Enter`) et via bouton
  - redirection vers `/search?q=...`
- Navigation:
  - ajout d'un lien `Search` dans le menu latéral (guest/client)
- Nouvelle page:
  - `GET /search`:
    - onglets `Products`, `Shops`, `Categories`
    - suggestions live (`api/search/suggestions`)
    - filtres principaux et pagination
    - affichage des resultats depuis `api/search/products`, `api/search/shops`, `api/search/categories`
- Ecrans existants migrés vers Search API:
  - Home: chargements produits/boutiques basés sur `api/search/*`
  - Catalog Shops: liste basée sur `api/search/shops`
  - Shop Details: produits boutique basés sur `api/search/products`
- Styles:
  - ajout de styles dédiés a l’UX de recherche (`segments`, suggestions, metrics categories, bouton topbar)

## Modifications mobile Flutter

- Ajout d'un module de recherche:
  - modèles: `core/models/search.dart`
  - client API: `core/api/search_api.dart`
  - provider DI: `searchApiProvider`
- Nouveau screen:
  - `features/search/search_screen.dart`
  - route: `/search` (query param `q`)
  - onglets `Products`, `Shops`, `Categories`
  - filtres et pagination simplifiés
- Home mobile:
  - la barre de recherche redirige vers `/search?q=...` (au lieu de filtrer localement la home)

## Tests manuels recommandés

1. Web top bar
   - Saisir une requete dans la top bar, valider `Enter`
   - verifier redirection vers `/search?q=...`

2. Web page `/search`
   - tester onglets `Products`, `Shops`, `Categories`
   - verifier suggestions sous le champ de recherche
   - tester filtres produits (tri, stock, promo, categorie)
   - tester filtres boutiques (tri, verified/recommended, country)
   - verifier pagination produits/boutiques

3. Web pages existantes
   - Home: verifier chargement sections produits/boutiques
   - `/shops`: verifier recherche/filtre toujours fonctionnels
   - `/shops/{id}`: verifier recherche produits boutique

4. Mobile Flutter
   - depuis Home, soumettre une recherche, verifier navigation vers `/search`
   - dans Search mobile, verifier onglets et filtres
   - verifier ouverture fiche produit et produits boutique
   - verifier pagination prev/next

## Validation technique

- Web build:
  - `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` -> OK
- Flutter analyze ciblé:
  - `flutter analyze lib/features/search/search_screen.dart lib/core/api/search_api.dart lib/core/models/search.dart lib/core/router/app_router.dart lib/core/providers.dart lib/features/home/home_screen.dart` -> OK
