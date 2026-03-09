# DM60-SEC-01 - OWASP Top 10 Review

Date: 2026-03-06

## Scope
- API auth and authorization flows
- file uploads (product/shop/profile/application)
- injection exposure (query patterns)
- CSRF posture (web state-changing endpoints)
- security headers and transport defaults

## Key findings and remediations

1. A01 Broken Access Control
- Status: mostly covered by `[Authorize]` and role attributes on admin endpoints.
- Action done: no weakening introduced; upload endpoints remain authenticated.

2. A02 Cryptographic Failures
- Risk found: secrets committed in `appsettings.*`.
- Action done: cleartext secrets removed from tracked appsettings and replaced by `CHANGE_ME_*` placeholders.

3. A03 Injection
- Status: main data access uses EF LINQ.
- Action done: no raw SQL introduced; no dynamic SQL added in this change.

4. A05 Security Misconfiguration
- Risk found: API could run with wildcard CORS outside development if origins missing.
- Action done: production now fails fast when `Cors:AllowedOrigins` is not configured.
- Action done: API security headers middleware added (`nosniff`, `DENY`, CSP for `/api`, HSTS when HTTPS).

5. A07 Identification and Authentication Failures
- Status: JWT auth + rate limiting already present.
- Action done: production startup validation added for critical security config (`Jwt:Key`, connection string, setup key, provider secrets consistency).

6. A08 Software and Data Integrity Failures
- Risk found: upload validation duplicated and weakly centralized.
- Action done: centralized `UploadSecurityValidator` added with:
  - max file size checks,
  - MIME/extension allow-list,
  - file signature (magic bytes) verification for PNG/JPEG/WEBP/PDF.

7. A09 Security Logging and Monitoring Failures
- Status: correlation id + error envelope already available.
- Action done: no regression introduced.

8. A10 SSRF
- Status: media proxy host allow-list already enforced (`api host only`).
- Action done: no regression introduced.

## Files changed
- `DoorMarket.Api/Program.cs`
- `DoorMarket.Api/Middlewares/SecurityHeadersMiddleware.cs`
- `DoorMarket.Api/Security/UploadSecurityValidator.cs`
- `DoorMarket.Api/Controllers/ProductsController.cs`
- `DoorMarket.Api/Controllers/ShopsController.cs`
- `DoorMarket.Api/Controllers/ShopApplicationsController.cs`
- `DoorMarket.Api/Controllers/MeController.cs`
- `DoorMarket.Infrastructure/Storage/LocalFileStorage.cs`
- `DoorMarket.Web/Program.cs`

## Manual verification checklist
- Upload PNG/JPEG/WEBP valid files -> `200`.
- Upload renamed invalid file (ex: `.jpg` containing text) -> `400`.
- Upload oversized file (>10MB) -> `400`.
- API response headers contain:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: no-referrer`
- Start API in Production without `Cors:AllowedOrigins` -> startup failure.
- Start API in Production with placeholder `Jwt:Key` -> startup failure.
