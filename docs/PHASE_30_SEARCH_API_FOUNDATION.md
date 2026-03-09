# Phase 30 - Search API Foundation

Date: 2026-03-02

## Scope delivered

- `GET /api/search/suggestions`
  - Unified suggestions across `Product`, `Shop`, `Category`
  - Relevance ranking (exact > prefix > contains)
  - Promotion and verification boost
  - Deduplication and limit cap

- `GET /api/search/products`
  - Filters: query, price range, in-stock, promoted, shop, category, city, country, rating
  - Sort: relevance, `price_asc`, `price_desc`, `rating_desc`, `newest`
  - Pagination with `PagedResult<ProductDto>`
  - Promotion-aware effective price and promotion metadata

- `GET /api/search/shops`
  - Filters: query, verified only, rating minimum, category, recommended, city, country
  - Sort: relevance, `rating_desc`, `newest`, `popular_desc`
  - Pagination with `PagedResult<ShopDto>`
  - Public image URL normalization

- `GET /api/search/categories`
  - Filters: query, country tag
  - Returns active discovery categories with counts:
    - `ActiveProductsCount`
    - `ActiveShopsCount`
  - Ordered by activity

## Validation rules

- `products`
  - `minPrice >= 0`
  - `maxPrice >= 0`
  - `minPrice <= maxPrice`
  - `ratingMin in [0..5]`

- `shops`
  - `ratingMin in [0..5]`

## Manual API checks (Postman / Swagger)

1. Suggestions relevance
   - `GET /api/search/suggestions?q=app&limit=12`
   - Confirm prefix matches rank before contains matches.

2. Product promotion + stock filter
   - `GET /api/search/products?q=app&inStockOnly=true&promotedOnly=true`
   - Confirm only active in-stock promoted products are returned.

3. Product price ordering
   - `GET /api/search/products?sort=price_asc`
   - Confirm `items[].effectivePrice` is ascending.

4. Shop quality filter
   - `GET /api/search/shops?ratingMin=4`
   - Confirm low-rated shops are excluded.

5. Shop location filter
   - `GET /api/search/shops?city=lub&countryTag=cd`
   - Confirm only matching location is returned.

6. Category discovery
   - `GET /api/search/categories`
   - Confirm categories are ordered by active product/shop counts.

7. Category query
   - `GET /api/search/categories?q=appl`
   - Confirm only matching categories with active products are returned.

## Automated test coverage

- `DoorMarket.Tests/SearchControllerTests.cs`
  - Suggestions ranking, short query guard, max limit cap
  - Product filters/sort/rating behavior
  - Shop filters/location behavior
  - Category query and activity ordering

