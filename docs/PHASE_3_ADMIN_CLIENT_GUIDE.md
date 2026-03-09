# DoorMarket - Phase 3 Guide (Admin + Client)

Date: 2026-03-01

## Objectif phase 3
- Rendre le pilotage marketing plus clair pour les administrateurs.
- Rendre les workflows de reconciliation/payout plus lisibles.
- Uniformiser le rendu visuel des cards dans le dashboard admin.
- Donner une documentation d'utilisation exploitable pour l'equipe.

## Nouveautes fonctionnelles

### 1) Marketing admin: statuts, filtres et KPI
- Nouvel endpoint: `GET /api/admin/marketing/banners/summary`
  - Retourne: `Total`, `Live`, `Planned`, `Expired`, `Inactive`, `Impressions`, `Clicks`, `CtrPercent`.
- Endpoint liste enrichi: `GET /api/admin/marketing/banners`
  - Nouveau filtre query `status`: `live|planned|expired|inactive`.
- Validation creation/edition banniere:
  - `StartAtUtc <= EndAtUtc`
  - `TargetUrl` doit etre `http://` ou `https://` (si renseignee).

### 2) Web admin marketing: meilleure lisibilite operationnelle
- Barre de filtres:
  - Recherche (`q`)
  - Filtre statut (All/Live/Planned/Expired/Inactive)
- Cartes KPI en tete:
  - Live, Planned, Expired, CTR
- Table banniere:
  - Badge de statut calculé (Live/Planned/Expired/Inactive)
  - Stats enrichies avec CTR (`impressions / clicks / ctr%`)
- Validation UI avant sauvegarde:
  - Blocage si date debut > date fin.

### 3) Reconciliation admin: clarte du workflow payout
- Filtre de la liste payout par statut (All/Draft/Approved/Paid/Reversed).
- Affichage des statuts en badges visuels.

### 4) Design dashboard admin: cards homogenes
- KPI cards avec hauteur minimale harmonisee.
- Panels "Top Products / Top Shops / Top Categories" avec hauteur unifiee.
- Ajout de classes CSS dediees:
  - `dm-admin-kpi-card`
  - `dm-admin-panel`
  - `dm-status-badge*`

## Parcours d'utilisation (admin)

### A. Piloter une campagne marketing
1. Aller sur `/admin/marketing`.
2. Creer une banniere avec:
   - Titre
   - Date debut/fin valides
   - Cible optionnelle (langue, ville, zone, categorie, boutique)
3. Sauvegarder.
4. Utiliser les filtres pour controler les statuts (`Live`, `Planned`, etc.).
5. Lire les KPI pour le suivi de performance (CTR).

### B. Suivre les payouts
1. Aller sur `/admin/reconciliation`.
2. Filtrer par periode et statut payout.
3. Verifier les montants et transitions:
   - `Draft -> Approved -> Paid -> Reversed`
4. Confirmer les ecarts de reconciliation avec les KPI bank.

## Parcours d'utilisation (client)

### A. Visibilite marketing
1. Les bannieres exposees au client sont celles actives dans leur fenetre de validite.
2. Les impressions/clicks alimentent les stats admin.

### B. Commande et commissions
1. Le client passe commande normalement.
2. Les commissions platforme (flat/percent) restent transparentes cote client mais impactent la reconciliation admin.

## Check-list manuelle phase 3

1. Ouvrir `/admin/marketing` et verifier que les KPI s'affichent.
2. Changer le filtre statut et confirmer que la liste change.
3. Creer une banniere avec date invalide (`start > end`) et verifier le blocage.
4. Creer/editer une banniere avec `TargetUrl` non `http/https` et verifier l'erreur API.
5. Verifier que les badges statut changent correctement selon les dates.
6. Verifier la colonne stats: `impressions / clicks (ctr%)`.
7. Ouvrir `/admin/reconciliation` et tester le filtre `Payout status`.
8. Verifier l'affichage badge pour `Draft`, `Approved`, `Paid`, `Reversed`.
9. Ouvrir `/admin/dashboard` et verifier que les cards KPI ont des tailles visuellement coherentes.
10. Verifier que les 3 panels Top* ont des hauteurs uniformes sur desktop.

## Notes techniques
- Les changements de phase 3 restent compatibles avec phase 2 (payout workflow + commissions flat/percent).
- Aucun changement de schema DB requis pour la phase 3 (hors phase 2 deja migree).
