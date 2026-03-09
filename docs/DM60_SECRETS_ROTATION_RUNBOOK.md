# DM60-SEC-02 - Secrets Management and Rotation Runbook

Date: 2026-03-06

## Objective
- Keep secrets out of source code.
- Rotate credentials with controlled downtime risk.
- Enforce automated checks before merge/release.

## Secret sources to use
- Local dev: `.NET User Secrets` (API project has `UserSecretsId`).
- Production: environment variables or external secret manager.

## Required keys (production)
- `ConnectionStrings__DefaultConnection`
- `Jwt__Key` (>= 32 chars)
- `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, ...
- `AdminSetup__Key` when `AdminSetup__Enabled=true`
- If SMTP enabled:
  - `Smtp__Username`, `Smtp__Password`, `Smtp__FromEmail`
- If Support SMTP enabled:
  - `Support__Smtp__Username`, `Support__Smtp__Password`, `Support__Smtp__FromEmail`
- If Stripe enabled:
  - `Stripe__SecretKey`, `Stripe__WebhookSecret`
- If PayPal enabled:
  - `PayPal__ClientId`, `PayPal__ClientSecret`

## Rotation procedure
1. Generate new secret in provider (DB, SMTP, Stripe, PayPal, JWT).
2. Store new value in secret manager / env vars.
3. Deploy to staging with new value.
4. Run smoke tests:
   - auth/login,
   - checkout + payment,
   - notification email.
5. Deploy production.
6. Revoke old secret.
7. Record change in incident/change log.

## Local developer setup example
```powershell
cd DoorMarket.Api
dotnet user-secrets set "Jwt:Key" "YOUR_LONG_RANDOM_KEY"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Password=...;"
dotnet user-secrets set "Smtp:Password" "..."
```

## CI secret check
- Script: `scripts/security/check-secrets.ps1`
- Workflow: `.github/workflows/security-secrets-scan.yml`
- Behavior:
  - scan source files for hardcoded credentials/tokens/passwords,
  - fail build on suspicious literals not marked as placeholders.

## Notes
- `appsettings*.json` must only contain placeholders (`CHANGE_ME_*`) for sensitive values.
- Production startup now fails fast on missing/placeholder critical secrets.
