# Sprint 8 - Checkout Observability Temps Reel + Alertes Paiement + A/B UX

## Objectif
- Mettre en place une observabilite checkout exploitable en temps reel.
- Detecter automatiquement les degradations qualite paiement.
- Mesurer les performances A/B UX checkout (groupes A/B).

## Backend (API + DB)

### Nouveau modele de donnees
- `DoorMarket.Domain/Entities/CheckoutAnalyticsEvent.cs`
- `DoorMarket.Infrastructure/Persistence/Configurations/CheckoutAnalyticsEventConfiguration.cs`
- `DoorMarket.Infrastructure/Persistence/DoorMarketDbContext.cs` (DbSet ajoute)
- Migration EF:
  - `DoorMarket.Infrastructure/Persistence/Migrations/20260304154410_AddCheckoutObservabilityAnalytics.cs`

### Correctifs migration SQL Server (blocages cascades)
- `DoorMarket.Infrastructure/Persistence/Configurations/ShopReviewHelpfulVoteConfiguration.cs`
- `DoorMarket.Infrastructure/Persistence/Migrations/20260304005426_AddPhase47ReviewsReturnsLoyalty.cs`
- `DoorMarket.Infrastructure/Persistence/Configurations/CommissionRuleConfiguration.cs`
- `DoorMarket.Infrastructure/Persistence/Migrations/20260304141344_AddDynamicCommissionRules.cs`
- Ajustements designers/snapshot associes pour coherence EF.
- Resultat: `dotnet ef database update --project DoorMarket.Infrastructure --startup-project DoorMarket.Api` s'execute jusqu'au bout.

### Services ajoutes
- `DoorMarket.Api/Services/CheckoutObservabilityEvents.cs`
- `DoorMarket.Api/Services/CheckoutObservabilityService.cs`
- `DoorMarket.Api/Services/CheckoutObservabilityCalculator.cs`

### Controllers ajoutes
- `DoorMarket.Api/Controllers/CheckoutAnalyticsController.cs`
  - `POST /api/checkout/analytics/track`
- `DoorMarket.Api/Controllers/AdminCheckoutObservabilityController.cs`
  - `GET /api/admin/checkout-observability/snapshot`
  - `GET /api/admin/checkout-observability/realtime`
  - `GET /api/admin/checkout-observability/alerts`
  - `GET /api/admin/checkout-observability/ab-ux`

### Instrumentation serveur
- `DoorMarket.Api/Controllers/OrdersController.cs`
  - tracking evenement `checkout_order_created` lors de la creation commande.
- `DoorMarket.Api/Controllers/PaymentsController.cs`
  - tracking des etapes paiement:
    - initiation success/fail
    - confirmation success/fail
    - erreurs de redirection et de provider

## Web Admin

### Dashboard enrichi
- `DoorMarket.Web/Components/Pages/Admin/Dashboard.razor`
- Ajout d’un bloc "Checkout Real-Time Observability" avec:
  - KPI checkout/paiement (submit, orders, success rate, drop rate, p50/p95)
  - alertes qualite paiement (warning/critical)
  - tableau qualite par provider
  - tableau A/B UX checkout (overall conversion + uplift vs control)
  - buckets temps reel (15 min)

## Web Checkout (Client)

- `DoorMarket.Web/Components/Pages/Orders/Checkout.razor`
- Ajouts:
  - session analytics checkout en `SessionStorage`
  - attribution groupe A/B (`checkout_ux_v1`)
  - tracking UX:
    - `checkout_viewed`
    - `checkout_payment_method_selected`
    - `checkout_submit_clicked`
    - `checkout_order_created`
    - `checkout_payment_redirect_opened`
    - `checkout_payment_redirect_failed`
    - `checkout_payment_initiation_failed`
    - `checkout_payment_confirmed`
    - `checkout_payment_failed`

## Flutter Mobile

### Nouveaux composants
- `DoorMarket.Flutter/doormarket_flutter/lib/core/api/checkout_analytics_api.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/core/services/checkout_observability_service.dart`
- `DoorMarket.Flutter/doormarket_flutter/lib/core/providers.dart` (providers ajoutes)

### Instrumentation checkout mobile
- `DoorMarket.Flutter/doormarket_flutter/lib/features/checkout/checkout_screen.dart`
- Ajouts:
  - attribution session + groupe A/B persistants (SharedPreferences)
  - tracking des etapes checkout/paiement (meme taxonomie que web)
  - tracking redirections externes et erreurs de fallback provider

## Validation technique executee
- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj` : OK
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj` : OK
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj` : OK (123/123)
- `flutter analyze` : OK
- `flutter test` : OK

## Checklist manuelle recommandee

1. Appliquer la migration:
   - `dotnet ef database update --project DoorMarket.Infrastructure --startup-project DoorMarket.Api`
2. Web checkout:
   - Ouvrir `/checkout`, changer de mode de paiement, lancer une commande.
   - Verifier que paiement Stripe/PayPal ouvre le provider.
3. Flutter checkout:
   - Ouvrir checkout, changer provider, finaliser une commande.
   - Verifier fallback externe/mobile money et messages.
4. Admin dashboard:
   - Ouvrir `/admin/dashboard`.
   - Verifier bloc "Checkout Real-Time Observability" rempli.
   - Verifier alertes quand taux de succes baisse ou echecs redirect augmentent.
5. A/B UX:
   - Verifier la presence de lignes `A` et `B` dans `ab-ux` apres trafic.
