# DoorMarket - Phase 5 (Checkout Readiness + Operations Snapshot)

Date: 2026-03-01

## Objectif
- Reduire les echec checkout cote client en detectant les blocages avant passage commande.
- Donner a l'admin une vue operationnelle rapide (conversion checkout, backlog payout, stock, marketing).

## 1) Client checkout readiness

### Nouvel endpoint
- `POST /api/cart/pre-checkout`

### Payload
```json
{
  "deliveryZoneId": "00000000-0000-0000-0000-000000000000",
  "promoCode": "DOOR10",
  "paymentProvider": "paypal",
  "prepaidCardCode": null,
  "requireDeliveryZone": true
}
```

### Verification effectuee
- panier vide
- produit absent/inactif
- boutique non verifiee
- stock insuffisant
- panier multi-devises
- zone de livraison invalide ou manquante
- code carte prepayee manquant si provider = `PrepaidCard`

### Estimation retournee
- `subtotal`
- `discount`
- `deliveryFee`
- `totalEstimate`
- `blockingIssues[]` + `warnings[]`

### UI web client
- Page panier:
  - affichage des blocages avant checkout
  - affichage des warnings non bloquants
  - bouton checkout desactive si blocage

## 2) Admin operations snapshot

### Nouvel endpoint
- `GET /api/admin/operations/snapshot?hours=24&staleCartHours=24`

### Metriques retournees
- commandes: creees, payees, failed
- conversion: `cartUsersActive`, `checkoutUsers`, `checkoutUserConversionRate`
- carts: `cartsWithItems`, `staleCarts`
- finance: backlog payouts (count + amount)
- catalogue: produits actifs en rupture / low stock
- onboarding et marketing: candidatures shop en attente, bannieres live

### UI web admin
- Dashboard admin:
  - nouveau bloc `Operations Snapshot (last 24h)`
  - metriques clefs visibles sans passer par plusieurs ecrans

## 3) Fiabilite technique
- Correctif ajoute pour calcul `payoutBacklogAmount` compatible multi-provider EF (SQLite + SQL Server).
- Les tests auto phase 5 couvrent:
  - pre-checkout (ready, blocages multi-devises/prepaid/zone, panier absent)
  - operations snapshot (metriques attendues + clamp de fenetre)

## 4) Checklist tests manuels

1. Cart client:
   - panier vide -> message blocage `empty_cart` et checkout desactive.
2. Cart client:
   - ajouter produit stock insuffisant -> blocage `stock_insufficient`.
3. Cart client:
   - panier devise mixte -> blocage `mixed_currency`.
4. Cart client:
   - provider `PrepaidCard` sans code -> blocage `prepaid_code_required`.
5. Cart client:
   - promo valide (`DOOR10`) + zone active -> estimation correcte et checkout actif.
6. Cart client:
   - promo invalide -> warning `promo_not_applied`, sans blocage.
7. Admin dashboard:
   - verifier affichage du bloc `Operations Snapshot` et coherence des compteurs commandes.
8. Admin dashboard:
   - creer payouts Draft/Approved/Paid puis verifier backlog count/montant.
9. Admin dashboard:
   - mettre un produit actif a `StockQty=0` puis `StockQty=3` et verifier rupture/low stock.
10. Admin dashboard:
   - creer banniere active live et candidature `Submitted` puis verifier compteurs.
