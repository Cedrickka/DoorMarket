# DM60-QA-01 - Campagne regression complete Web/Mobile/API

Date: 2026-03-06

## Scripts et commandes

- Script principal:
  - `scripts/qa/run_dm60_regression.ps1`

Execution:
```powershell
./scripts/qa/run_dm60_regression.ps1 -Configuration Release
```

## Couverture campagne

### API
- suite `DoorMarket.Tests` complete
- erreurs API standardisees + correlation id
- auth/search/checkout rate limit

### Web
- `WebCheckoutSmokeTests`
- parcours commande/paiement/reprise
- verification panier vide apres paiement

### Mobile Flutter
- `mobile_checkout_smoke_test.dart`
- commande + mobile money retry + reprise

## Matrice manuelle minimale (Go/No-Go)

1. Client web:
   - recherche produit
   - ajout panier
   - checkout
   - paiement
2. Client mobile:
   - panier
   - checkout
   - paiement/retry
3. Admin:
   - dashboard
   - reconciliation
   - commissions
   - notifications

## Evidence a conserver

- fichiers `trx` dans `artifacts/qa`
- captures ecrans des flows critiques
- ids commandes de test
- logs serveurs (correlation ids)
