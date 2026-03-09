# DoorMarket - Guide d'utilisation (Web + Mobile)

## 1. Objectif
Ce guide décrit les parcours principaux pour:
- Client
- Boutique (shop)
- Administrateur

## 2. Parcours Client

### 2.1 Recherche et découverte
1. Utiliser la barre de recherche (home/catégories).
2. Affiner avec filtres (prix, note, disponibilité, promotion).
3. Ouvrir le détail produit en cliquant la carte.
4. Ajouter rapidement au panier avec le bouton d'ajout.

### 2.2 Panier et checkout
1. Vérifier quantités, adresse, zone de livraison.
2. Confirmer le mode de paiement.
3. Valider la commande.
4. Suivre le statut dans `Commandes`.

### 2.3 Wishlist et récemment consultés
1. Ajouter/supprimer des articles de la wishlist.
2. Revenir sur les articles consultés pour reprise rapide.

### 2.4 Notifications
1. Ouvrir l'écran notifications.
2. Utiliser refresh (tirer vers le bas) pour recharger.
3. Ouvrir la commande liée depuis une notification quand disponible.

## 3. Parcours Boutique (Shop)

### 3.1 Produits
1. Créer/modifier produit.
2. Définir stock et disponibilité.
3. Définir commission produit si nécessaire.

### 3.2 Commandes
1. Ouvrir la commande.
2. Mettre à jour le statut d'exécution selon le workflow.
3. Vérifier l'historique des changements.

## 4. Parcours Admin

### 4.1 Dashboard
- Consulter KPI ventes, funnel conversion, retours, campagnes.
- Vérifier opérations (payout backlog, notifications, carts recovery).

### 4.2 Marketing
- Créer des bannières/campagnes.
- Filtrer et exporter les KPI/ROI.
- Suivre A/B uplift et recommandations automatiques.

### 4.3 Reconciliation
- Générer et suivre payouts.
- Exécuter transitions (`Draft -> Approved -> Paid -> Reversed`).
- Utiliser l'assistant reconciliation pour détecter anomalies.

### 4.4 Commissions dynamiques
- Aller sur `/admin/commissions`.
- Créer des règles par scope (`Global`, `Shop`, `Category`, `Product`).
- Utiliser `preview` pour vérifier la commission appliquée.

## 5. Mobile (Flutter) - règles UX
- Entêtes cohérents via composants partagés.
- Cartes produits compactes et lisibles.
- Recherche en filtrage direct quand applicable.
- Pull-to-refresh sur écrans dynamiques.
- Messages explicites pour états vides et erreurs.

## 6. Dépannage utilisateur (niveau 1)
- `400` sur formulaire adresse: vérifier zone de livraison active et champs obligatoires.
- Recherche vide: vérifier API search disponible et session authentifiée si endpoint protégé.
- Notifications vides: vérifier qu'il existe des événements et lancer refresh.
- Paiement non finalisé: vérifier retour provider et statut de commande.

## 7. Bonnes pratiques support
- Toujours demander:
  - ID utilisateur
  - ID commande (si concerné)
  - date/heure exacte
  - capture écran
- Reproduire d'abord sur web admin/API avant escalade backend.

## 8. Lancement controle (Go-Live)
- Checklist execution 30 jours: `docs/PHASE_60_GO_LIVE_30D_CONTROLLED_RELEASE.md`
- Backlog tickets pret Jira: `docs/DM60_BACKLOG_JIRA_GO_LIVE_30D.md`
