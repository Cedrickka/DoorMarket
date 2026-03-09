# DoorMarket - Phase 4 (Automation Marketing + Finance Controls)

Date: 2026-03-01

## Objectif
- Automatiser des taches marketing repetitives pour l'equipe admin.
- Durcir les controles financiers (payouts multi-devises).
- Accelerer la gestion des commissions via mise a jour en masse.

## 1) Marketing automation

### Nouvel endpoint
- `POST /api/admin/marketing/automation/run`

### Payload
```json
{
  "disableExpired": true,
  "autoPrioritizeLiveByCtr": true,
  "minImpressionsForCtr": 100
}
```

### Effets
- Desactive automatiquement les bannieres actives deja expirees.
- Reordonne les bannieres live par performance CTR (avec seuil minimum d'impressions).
- Retourne le nombre de bannieres modifiees.

### UI admin
- Page `/admin/marketing`: bouton `Run automation`.
- Affiche un message de resultat (`expired disabled`, `live reprioritized`).

## 2) Payouts: regles plus strictes

### Controle anti-chevauchement
- Creation payout refusee si la periode chevauche un payout actif existant pour la meme boutique + devise.

### Controle date de paiement
- `PaidOutAtUtc` refuse si avant le debut de periode.
- `PaidOutAtUtc` refuse si avant la date d'approbation.

### Nouveau resume finance
- Endpoint: `GET /api/admin/reconciliation/payouts/summary`
- Retour par devise:
  - volumes par statut (`Draft/Approved/Paid/Reversed`)
  - `TotalNetToPay`
  - `TotalPaidOut`

### UI admin
- Page `/admin/reconciliation`:
  - tableau de synthese payouts par devise.
  - filtre payout status deja present.

## 3) Commissions: mise a jour bulk

### Nouvel endpoint
- `POST /api/admin/products/fees/bulk-by-ids`

### Usage
- Applique un mode `Flat` ou `Percent` a une liste de produits.
- Option `onlyActive=true` pour ignorer les produits inactifs.
- Validation stricte:
  - `Flat >= 0` et `Flat <= Price`
  - `Percent > 0` et `Percent <= 100`

### UI admin
- Page `/admin/product-fees`:
  - bloc `Bulk commission update (current page)`.
  - applique une commission a tous les produits affiches sur la page courante.

## 4) Checklist tests manuels

1. Marketing:
   - lancer `Run automation` avec des bannieres expirees et verifier qu'elles passent inactives.
2. Marketing:
   - verifier que l'ordre d'affichage live change selon CTR apres automation.
3. Reconciliation:
   - tenter creation payout avec periode chevauchante -> doit retourner conflit.
4. Reconciliation:
   - marquer paid avec date avant periode -> doit etre refuse.
5. Reconciliation:
   - verifier le tableau de synthese par devise (counts + montants).
6. Product fees:
   - appliquer bulk `Percent` sur la page courante -> verifier persistance.
7. Product fees:
   - tester `only active` et verifier que les produits inactifs ne changent pas.
8. Product fees:
   - tester valeur invalide (`Percent=0`, `Flat>Price`) -> verifier erreur.

## 5) Couverture tests auto
- Ajout de tests controller pour:
  - automation marketing
  - contraintes payout (overlap/date)
  - bulk commissions produits
