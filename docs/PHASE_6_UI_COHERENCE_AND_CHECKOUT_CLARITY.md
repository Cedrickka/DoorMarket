# DoorMarket - Phase 6 (UI Coherence + Checkout Clarity)

Date: 2026-03-01

## Objectif
- Uniformiser l'experience visuelle dashboard/admin/shop.
- Corriger l'incoherence de taille des cards KPI.
- Clarifier le resume panier avec les donnees pre-checkout.

## 1) Theme global unifie

### Theme MudBlazor
- `MainLayout.razor`:
  - ajout d'un theme global `MudTheme` avec palette DoorMarket (bleu/orange) pour eviter le style par defaut.
  - fonds, textes, drawer, appbar et radius harmonises.

### Typographie
- `App.razor`:
  - passage de la police web de `Inter` vers `Manrope`.

## 2) Design system - classes communes

### CSS global
- `wwwroot/css/doormarket.css`:
  - nouveaux tokens et classes pour coherence:
    - animation d'entree (`dm-view-enter`)
    - structure KPI (`dm-kpi-label`, `dm-kpi-value`, `dm-kpi-meta`)
    - variantes de tonalite (`dm-kpi-tone-success`, `warning`, `danger`)
    - panneau admin (`dm-admin-panel`, `dm-admin-panel-title`, `dm-admin-panel-sub`)
    - operations snapshot en tuiles (`dm-ops-grid`, `dm-ops-card`)
    - resume panier (`dm-summary-card`, `dm-summary-row`, `dm-summary-total`)

## 3) Dashboard admin

### Fichier
- `Components/Pages/Admin/Dashboard.razor`

### Changements
- cards KPI harmonisees:
  - hauteur, rythme vertical, typo metrique uniforme.
- sections "Top Products / Shops / Categories":
  - titres de panneau normalises.
- bloc "Operations Snapshot":
  - passage en grille de tuiles homogenes (au lieu d'un simple flux de textes).

## 4) Dashboard shop

### Fichier
- `Components/Pages/Shop/Dashboard.razor`

### Changements
- cards KPI normalisees avec les memes regles visuelles que l'admin.
- couleur "Revenue paid" alignee sur ton `success` (plus lisible metier).
- panneaux "Quick Actions" et "Shop Health" alignes sur un composant panel commun.

## 5) Panier (clarte fonctionnelle)

### Fichier
- `Components/Pages/Cart/Cart.razor`

### Changements
- resume financier:
  - utilise les valeurs pre-checkout si disponibles (`subtotal`, `discount`, `delivery`, `totalEstimate`).
- blocage checkout:
  - condition centralisee via `CheckoutBlocked`.
- correction de coherence runtime:
  - apres update qty/suppression article, recalcul automatique du `pre-checkout`.
  - evite les etats stale ou les infos summary non synchronisees.

## 6) Validation technique

- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj`: OK
- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj`: OK
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj`: OK (`27/27`)

## 7) Checklist tests manuels

1. Admin dashboard:
   - verifier que toutes les cards KPI ont meme hauteur sur desktop.
2. Admin dashboard:
   - verifier le bloc "Operations Snapshot" en tuiles regulieres (desktop + mobile).
3. Shop dashboard:
   - verifier homogenei te des 4 KPI (hauteur, espacement, lisibilite).
4. Shop dashboard:
   - verifier que "Revenue paid" apparait avec accent de succes (vert).
5. Cart:
   - modifier la quantite d'un item et verifier mise a jour immediate du summary.
6. Cart:
   - supprimer un item et verifier que les blocages/warnings pre-checkout se recalculent.
7. Cart:
   - confirmer que le bouton checkout se desactive seulement si `blockingIssues` existe.
8. Global:
   - verifier la nouvelle palette du theme sur boutons, chips, appbar et drawer.
