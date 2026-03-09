# DoorMarket - Runbook d'exploitation

## 1. Périmètre
Ce runbook couvre l'exploitation de:
- API (`DoorMarket.Api`)
- Web (`DoorMarket.Web`)
- Mobile Flutter (client)
- Base SQL Server + jobs applicatifs

## 2. Pré-requis d'environnement
- .NET 8 SDK
- SQL Server accessible
- Variables sensibles configurées:
  - JWT key
  - SMTP
  - Stripe / Mobile Money
  - Firebase/APNs (si push activé)

## 3. Procédures de déploiement

### 3.1 Migration base
```powershell
dotnet ef database update --project DoorMarket.Infrastructure --startup-project DoorMarket.Api
```

### 3.2 Build de validation
```powershell
dotnet build DoorMarket.Api/DoorMarket.Api.csproj
dotnet build DoorMarket.Web/DoorMarket.Web.csproj
dotnet build DoorMarket.Tests/DoorMarket.Tests.csproj
```

### 3.3 Tests smoke
```powershell
dotnet test DoorMarket.Tests
```

## 4. Vérifications post-déploiement

### 4.1 API
- `GET /ping` retourne `pong`
- Auth login OK
- Endpoints admin accessibles selon rôle

### 4.2 Admin critique
- `/admin/dashboard` charge KPI
- `/admin/reconciliation` charge payouts + assistant
- `/admin/marketing` charge ROI + export
- `/admin/commissions` charge règles + preview

### 4.3 Client critique
- Recherche produit
- Ajout panier
- Checkout
- Historique commandes

## 5. Monitoring recommandé
- Latence API p50/p95 par endpoint critique
- Taux erreurs HTTP (4xx/5xx)
- Jobs:
  - cart recovery scheduler
  - marketing automation worker
- Notifications:
  - SMTP fail count
  - Push fail count
- Paiements:
  - succès/échec par provider

## 6. Incidents fréquents et actions

### 6.1 Erreurs EF migration
- Symptôme: conflit FK / multiple cascade paths.
- Action: remplacer cascade par `NoAction`/`SetNull` sur la relation concernée, regénérer migration.

### 6.2 Reconciliation incohérente
- Symptôme: écarts payout/bank.
- Action:
  1. Ouvrir assistant reconciliation.
  2. Traiter stale drafts, overlaps, partial paid.
  3. Vérifier références de paiement.

### 6.3 Search ou notifications vides
- Action:
  - vérifier données sources
  - vérifier filtres de période
  - valider logs API

### 6.4 Mobile freeze / skipped frames
- Action:
  - profiler (`flutter run --profile`, DevTools timeline)
  - réduire travail sync au build frame
  - lazy rendering listes longues

## 7. Sauvegarde / restauration
- Sauvegarde DB quotidienne + test de restauration hebdomadaire.
- Conserver exports opérationnels (ROI, reconciliation) pour audit.

## 8. Checklist d'escalade
Avant d'escalader:
1. Horodatage exact (UTC)
2. Endpoint/écran concerné
3. Payload / ID (orderId, payoutId, userId)
4. Stacktrace complète
5. Impact utilisateur (bloquant/non bloquant)

## 9. Contacts et ownership (à compléter)
- API owner:
- Web owner:
- Mobile owner:
- Ops/Infra owner:
