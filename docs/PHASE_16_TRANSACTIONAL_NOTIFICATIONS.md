# Phase 16 - Notifications Transactionnelles Paiement/Commande

## Goal
Mettre en place des notifications transactionnelles claires autour du cycle commande/paiement pour le client, sans casser le flux existant admin/boutique.

## Delivered

### API - Notifications client
- Nouveau service `IClientOrderNotificationService` / `ClientOrderNotificationService`:
  - `NotifyClientOrderCreatedAsync(orderId)`
  - `NotifyClientPaymentPaidAsync(orderId)`
  - `NotifyClientPaymentFailedAsync(orderId, reason)`
- Templates email transactionnels ajoutés:
  - commande créée (paiement en attente)
  - paiement confirmé
  - paiement échoué (avec raison si disponible)
- Liens dynamiques vers commande/support basés sur `App:WebBaseUrl`.

### API - Branches de déclenchement
- Checkout (`OrdersController`):
  - envoi notification client "commande créée" juste après création de commande.
- Workflow paiement (`OrderPaymentWorkflowService`):
  - envoi notification client "paiement confirmé" lors de la première transition vers `Paid`.
  - envoi notification client "paiement échoué" lors de transition vers `Failed`.
  - anti-doublon sur transitions pour éviter spam en cas d’appels répétés/webhooks redondants.

### API - DI
- Enregistrement du service dans `Program.cs`:
  - `AddScoped<IClientOrderNotificationService, ClientOrderNotificationService>()`

### Tests
- Nouveaux tests service notifications client:
  - `ClientOrderNotificationServiceTests`
- Nouveaux tests workflow paiement:
  - `OrderPaymentWorkflowServiceTests`
  - vérification anti-doublon sur `Paid` / `Failed`
  - vérification cas stock insuffisant (échec paiement + notif unique)

## Files
- `DoorMarket.Api/Services/ClientOrderNotificationService.cs`
- `DoorMarket.Api/Services/OrderPaymentWorkflowService.cs`
- `DoorMarket.Api/Controllers/OrdersController.cs`
- `DoorMarket.Api/Program.cs`
- `DoorMarket.Tests/ClientOrderNotificationServiceTests.cs`
- `DoorMarket.Tests/OrderPaymentWorkflowServiceTests.cs`

## Validation
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~ClientOrderNotificationServiceTests|FullyQualifiedName~OrderPaymentWorkflowServiceTests"`

## Manual Tests
1. Créer une commande via checkout (web ou mobile) et vérifier la réception de l’email "commande créée".
2. Payer la commande (Stripe/PayPal/Prepaid) et vérifier l’email "paiement confirmé".
3. Simuler un échec de paiement (webhook `async_payment_failed` ou cas stock insuffisant) et vérifier l’email "paiement échoué".
4. Rejouer le même webhook/endpoint de paiement et vérifier qu’aucun doublon d’email n’est envoyé.
5. Vérifier que les liens email ouvrent bien la commande et le support sur l’URL de front attendue.
