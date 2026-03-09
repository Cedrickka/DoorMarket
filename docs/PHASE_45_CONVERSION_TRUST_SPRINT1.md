# Phase 45 - Conversion & Trust Sprint 1

Date: 2026-03-02

## Objectif

Renforcer la confiance client et la conversion avec des mecanismes "e-commerce mature":
- avis "achat verifie"
- base retours/remboursements self-service
- base promo/coupon
- base relance panier abandonne

## Lot livre dans cette phase (45.1)

### Avis "achat verifie" (API + Web + Flutter)

- API:
  - ajout du champ `IsVerifiedPurchase` sur les avis boutique
  - calcul serveur de verification base sur commande payee + livree + item de la boutique
  - validation `OrderId` (si fourni) pour eviter les faux avis verifies
- Web:
  - badge `Achat verifie` affiche dans la section avis boutique
- Flutter:
  - mapping du champ `isVerifiedPurchase`
  - libelle `Achat verifie / Verified purchase` dans la liste d'avis boutique
- Tests:
  - cas positif: avis marque verifie avec achat eligible
  - cas negatif: `OrderId` invalide rejete

## Backlog Jira detaille - Sprint 1

### Epic

- `EPIC DM45-CONVERSION-TRUST-S1`

### Tickets

| ID | Lot | Ticket | Estimation | Dependance |
|---|---|---|---|---|
| DM45-REV-01 | API | Ajouter `IsVerifiedPurchase` sur `ShopReview` + migration EF | 0.5j | - |
| DM45-REV-02 | API | Calcul/verif achat verifie (paid+delivered+shop match) dans `POST /api/shops/{id}/reviews` | 1j | DM45-REV-01 |
| DM45-REV-03 | Web | Afficher badge `Achat verifie` dans `ShopDetails` | 0.5j | DM45-REV-02 |
| DM45-REV-04 | Flutter | Afficher badge `Achat verifie` dans `shop_products_screen` | 0.5j | DM45-REV-02 |
| DM45-REV-05 | QA | Tests API unitaires/integration avis verifies | 0.5j | DM45-REV-02 |
| DM45-RET-01 | API | Creer modele `ReturnRequest` (orderId, reason, status, amounts, timeline) | 1.5j | - |
| DM45-RET-02 | API | Endpoints client retours: create/list/details | 2j | DM45-RET-01 |
| DM45-RET-03 | API | Endpoints admin retours: approve/reject/refund-marked + journal | 2j | DM45-RET-02 |
| DM45-RET-04 | Web | Ecran client "Demander un retour" + suivi statut | 1.5j | DM45-RET-02 |
| DM45-RET-05 | Web | Ecran admin "Gestion retours/remboursements" | 2j | DM45-RET-03 |
| DM45-RET-06 | Flutter | Ecran mobile retour commande + suivi | 2j | DM45-RET-02 |
| DM45-CPN-01 | API | Base coupons (code, validite, budget, usage limit, scope) | 2j | - |
| DM45-CPN-02 | API | Endpoint validation coupon pre-checkout + calcul discount | 1.5j | DM45-CPN-01 |
| DM45-CPN-03 | Web+Flutter | Champ coupon checkout + feedback clair | 1.5j | DM45-CPN-02 |
| DM45-CRT-01 | API | Event panier abandonne + table + anti-spam window | 1j | - |
| DM45-CRT-02 | API | Job relance email/push panier abandonne (basic scheduler) | 1.5j | DM45-CRT-01 |
| DM45-OBS-01 | QA/Doc | Plan QA + guide support (retours/coupons/relance) | 1j | Tous |

## Validation executee (lot 45.1)

- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~ShopReviewsControllerTests"`: success
- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj`: success (warnings pre-existants)
- `flutter analyze lib/core/models/shops.dart lib/features/shops/shop_products_screen.dart`: success

## Tests manuels recommandes (lot 45.1)

1. Client avec commande payee+livree sur la boutique:
   - publier avis
   - verifier badge `Achat verifie` sur web et mobile
2. Client sans commande eligible:
   - publier avis
   - verifier absence du badge
3. Forcer un `orderId` non eligible:
   - verifier erreur API `400`
4. Modifier un avis existant apres achat eligible:
   - verifier conservation du statut `Achat verifie`

## Lot livre dans cette phase (45.2)

### Retours self-service - fondation API (DM45-RET-01 + DM45-RET-02)

- Domain/DB:
  - nouvelle entite `ReturnRequest`
  - relation avec `Order` et `User`
  - migration EF `AddReturnRequests`
- API client:
  - `POST /api/returns` (creer une demande de retour)
  - `GET /api/returns/mine` (liste paginee des retours du client)
  - `GET /api/returns/{id}` (detail d'un retour client)
- Regles metier v1:
  - commande doit appartenir au client courant
  - commande doit etre `Paid` et `Delivered/Completed`
  - une seule demande ouverte par commande (`Requested` ou `Approved`)
  - montant demande <= restant remboursable (total - deja approuve/rembourse)
  - validation stricte `reason/comment`

## Validation executee (lot 45.2)

- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~ReturnsControllerTests|FullyQualifiedName~ShopReviewsControllerTests"`: success
- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj`: success

## Tests manuels recommandes (lot 45.2)

1. Creer un retour valide:
   - client connecte
   - commande payee + livree
   - `POST /api/returns` -> `201`
2. Demande en double sur meme commande:
   - refaire `POST /api/returns` sur la commande avec retour deja `Requested`
   - verifier `400`
3. Montant demande trop eleve:
   - `requestedAmount` > montant restant remboursable
   - verifier `400`
4. Lister les retours du client:
   - `GET /api/returns/mine?page=1&pageSize=20`
   - verifier pagination et ownership
5. Acces detail d'un retour d'un autre utilisateur:
   - `GET /api/returns/{id}` (non proprietaire)
   - verifier `404`

## Lot livre dans cette phase (45.3)

### Admin retours/remboursements + journal (DM45-RET-03)

- Journal:
  - nouvelle entite `ReturnRequestStatusHistory`
  - historique des transitions statut avec auteur/date/note
- API admin:
  - `GET /api/admin/returns` (liste paginee + filtres)
  - `GET /api/admin/returns/{id}` (detail)
  - `GET /api/admin/returns/{id}/history` (journal)
  - `POST /api/admin/returns/{id}/approve`
  - `POST /api/admin/returns/{id}/reject`
  - `POST /api/admin/returns/{id}/mark-refunded`
- Regles metier transitions:
  - `Requested -> Approved`
  - `Requested -> Rejected`
  - `Approved -> Refunded`
  - transitions invalides => `409 Conflict`

## Validation executee (lot 45.3)

- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminReturnsControllerTests|FullyQualifiedName~ReturnsControllerTests|FullyQualifiedName~ShopReviewsControllerTests"`: success
- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj`: success

## Tests manuels recommandes (lot 45.3)

1. Lister les retours admin:
   - `GET /api/admin/returns?page=1&pageSize=20`
   - verifier pagination + filtres `status/from/to/q`
2. Approuver un retour `Requested`:
   - `POST /api/admin/returns/{id}/approve`
   - verifier statut `Approved` + `approvedAmount`
3. Rejeter un retour `Requested`:
   - `POST /api/admin/returns/{id}/reject`
   - verifier statut `Rejected`
4. Marquer rembourse un retour `Approved`:
   - `POST /api/admin/returns/{id}/mark-refunded`
   - verifier statut `Refunded` + `refundedAtUtc`
5. Journal:
   - `GET /api/admin/returns/{id}/history`
   - verifier lignes des transitions avec ordre chrono

## Lot livre dans cette phase (45.4)

### Web client retours - demande + suivi (DM45-RET-04)

- Web client:
  - nouvelle page `/returns` (demande + suivi)
  - formulaire de creation retour:
    - selection commande eligible (`Paid` + `Delivered/Completed`)
    - raison obligatoire
    - commentaire optionnel
    - montant demande optionnel
  - suivi des demandes:
    - liste paginee avec statut, montant demande, montant approuve, note admin
  - integration parcours commande:
    - bouton "Retour/Return" depuis `/orders`
    - bouton "Demander un retour/Request return" depuis `/orders/{id}`
    - pre-selection de commande via query string `?orderId=...`
- Navigation:
  - ajout entree client `Returns/Retours` dans le menu principal
  - ajout entree dans le menu profil client

## Validation executee (lot 45.4)

- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj`: success

## Tests manuels recommandes (lot 45.4)

1. Flux complet depuis la page retours:
   - aller sur `/returns`
   - creer un retour valide
   - verifier apparition immediate dans "Suivi des retours"
2. Flux depuis la liste des commandes:
   - aller sur `/orders`
   - cliquer `Retour/Return` sur une commande eligible
   - verifier pre-selection de la commande sur `/returns`
3. Flux depuis le detail commande:
   - aller sur `/orders/{id}`
   - cliquer `Demander un retour/Request return`
   - verifier pre-selection `orderId`
4. Cas commande non eligible:
   - verifier absence du bouton retour ou blocage creation
5. Verification visuelle:
   - verifier statuts (`Requested/Approved/Rejected/Refunded`) et montants

## Lot livre dans cette phase (45.5)

### Web admin retours/remboursements - gestion operationnelle (DM45-RET-05)

- Web admin:
  - nouvelle page `/admin/returns`
  - filtres: `status`, `from`, `to`, `q`
  - tableau pagine des retours avec client, statut, raison, montants
  - panneau de traitement:
    - details retour/client
    - action `Approve` (montant approuve + note)
    - action `Reject` (note)
    - action `Mark refunded` (montant final + date + note)
  - historique de transition charge depuis `/api/admin/returns/{id}/history`
- Navigation:
  - ajout entree admin `Returns/Retours` dans le menu principal

## Validation executee (lot 45.5)

- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj`: success

## Tests manuels recommandes (lot 45.5)

1. Liste admin:
   - aller sur `/admin/returns`
   - tester filtres `status/from/to/q`
   - verifier pagination
2. Traitement `Requested -> Approved`:
   - selectionner un retour `Requested`
   - saisir note et montant (optionnel)
   - cliquer `Approve`
   - verifier statut + historique
3. Traitement `Requested -> Rejected`:
   - selectionner un retour `Requested`
   - cliquer `Reject`
   - verifier statut + historique
4. Traitement `Approved -> Refunded`:
   - selectionner un retour `Approved`
   - cliquer `Mark refunded`
   - verifier `refundedAtUtc` + historique
5. Controle transitions invalides:
   - tenter action non permise par statut
   - verifier message d'erreur UI/API

## Lot livre dans cette phase (45.6)

### Flutter retours client - demande + suivi mobile (DM45-RET-06)

- Flutter:
  - ajout du modele `ReturnRequestDto` + payload `CreateReturnRequest`
  - ajout client API `ReturnsApi`:
    - `POST /api/returns`
    - `GET /api/returns/mine`
    - `GET /api/returns/{id}`
  - nouvel ecran mobile `/returns`:
    - formulaire demande retour (commande, raison, commentaire, montant)
    - filtrage statut (`All/Requested/Approved/Rejected/Refunded`)
    - suivi pagine des demandes avec montants/note admin
  - integration navigation:
    - entree `Retours/Returns` dans le menu profil mobile
    - raccourci retour depuis liste commandes
    - raccourci retour depuis detail commande
    - preselection via query `?orderId=...`

## Validation executee (lot 45.6)

- `flutter analyze` (cible sur les fichiers modifies): success

## Tests manuels recommandes (lot 45.6)

1. Navigation profil:
   - ouvrir Profil -> `Retours/Returns`
   - verifier ouverture de l'ecran mobile retours
2. Navigation commandes:
   - depuis `/orders`, cliquer `Demander retour/Request return` sur commande eligible
   - verifier preselection commande sur `/returns`
3. Navigation detail commande:
   - depuis `/orders/{id}`, cliquer `Demander un retour/Request return`
   - verifier preselection commande
4. Soumission retour:
   - saisir raison + montant optionnel
   - verifier confirmation et affichage dans suivi
5. Filtrage suivi:
   - tester `Requested/Approved/Rejected/Refunded`
   - verifier pagination suivant/precedent

## Lot livre dans cette phase (45.7)

### Coupons - fondation API (DM45-CPN-01)

- Domain/DB:
  - nouvelle entite `Coupon` avec:
    - `code`
    - validite (`startsAtUtc`, `endsAtUtc`)
    - budget (`budgetAmount`)
    - limites d'usage (`usageLimitTotal`, `usageLimitPerUser`)
    - scope (`Global|Shop|Category|City|Country`)
    - parametrage discount (`Percent|Fixed`, `discountValue`, `maxDiscountAmount`)
  - configuration EF + index unique sur `Code`
  - migration EF: `20260302133305_AddCoupons`
- API admin coupons:
  - `GET /api/admin/coupons` (liste paginee)
  - `GET /api/admin/coupons/{id}` (detail)
  - `POST /api/admin/coupons` (creation)
  - `PUT /api/admin/coupons/{id}` (edition)
  - `POST /api/admin/coupons/{id}/activate`
  - `POST /api/admin/coupons/{id}/deactivate`
  - `DELETE /api/admin/coupons/{id}`
- Promo engine:
  - `PromoService` migre vers coupons DB (avec fallback legacy `DOOR10` / `WELCOME5`)
  - regles prises en charge:
    - active/inactive
    - fenetre de validite
    - montant minimum
    - limites budget/usage
    - scope coupon
  - usage calculee via `PromoAuditLogs` source `Checkout`

## Validation executee (lot 45.7)

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj`: success
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AdminCouponsControllerTests|FullyQualifiedName~PromoServiceTests|FullyQualifiedName~OrderServicePromoTests|FullyQualifiedName~CartPreCheckoutControllerTests"`: success

## Tests manuels recommandes (lot 45.7)

1. Creation coupon global:
   - `POST /api/admin/coupons` avec `DiscountType=Percent`
   - verifier `code` normalise en majuscules
2. Activation/desactivation:
   - `POST /api/admin/coupons/{id}/deactivate` puis `activate`
   - verifier changement `OperationalStatus`
3. Validation limite usage:
   - definir `usageLimitTotal=1`
   - appliquer coupon sur un checkout valide
   - verifier non-applicable au second checkout
4. Validation budget:
   - definir budget faible
   - appliquer coupon jusqu'a epuisement
   - verifier message budget epuise
5. Validation scope:
   - coupon `ScopeType=Shop` ou `Category`
   - verifier application uniquement si panier scope-compatible

## Lot livre dans cette phase (45.8)

### Validation coupon pre-checkout (DM45-CPN-02)

- API:
  - nouvel endpoint `POST /api/cart/validate-coupon`
  - request:
    - `code`
    - `deliveryZoneId` (optionnel)
    - `requireDeliveryZone` (optionnel)
  - response:
    - `isReady`
    - `promoCode`, `applied`, `message`
    - `currency`, `itemCount`, `subtotal`, `discount`, `deliveryFee`, `totalEstimate`
    - `blockingIssues`, `warnings`
- Regles:
  - validation stricte du code coupon
  - calcul discount via moteur coupons DB (avec fallback legacy)
  - calcul livraison si `deliveryZoneId` fourni
  - warning explicite si coupon non applique (`coupon_not_applied`)
  - journalisation audit promo source `CartValidateCoupon`

## Validation executee (lot 45.8)

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj`: success
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~CartCouponValidationControllerTests|FullyQualifiedName~CartPreCheckoutControllerTests|FullyQualifiedName~AdminCouponsControllerTests|FullyQualifiedName~PromoServiceTests|FullyQualifiedName~OrderServicePromoTests"`: success

## Tests manuels recommandes (lot 45.8)

1. Coupon valide:
   - `POST /api/cart/validate-coupon` avec code actif + panier eligible
   - verifier `applied=true`, `discount>0`, `totalEstimate` coherent
2. Coupon invalide:
   - code inconnu
   - verifier `applied=false` + warning `coupon_not_applied`
3. Validation livraison:
   - fournir `deliveryZoneId` valide
   - verifier `deliveryFee` injecte dans total
4. Zone requise:
   - `requireDeliveryZone=true` sans `deliveryZoneId`
   - verifier `blockingIssues` avec `delivery_zone_required`
5. Audit:
   - verifier insertion log `PromoAuditLogs` source `CartValidateCoupon`

## Lot livre dans cette phase (45.9)

### UX coupon checkout web + mobile (DM45-CPN-03)

- Web checkout (`/checkout`):
  - champ coupon avec actions explicites:
    - `Appliquer`: valide le code via `POST /api/cart/validate-coupon`
    - `Effacer`: retire le code applique et remet le calcul sans remise
  - feedback clair:
    - message succes/erreur depuis l'API
    - affichage montant remise (`discount`) quand applique
  - coherence calcul:
    - remise et total bases sur la reponse de validation coupon
    - coupon envoye a la creation commande seulement s'il est applique
  - revalidation forcee:
    - changement de zone de livraison => revalidation coupon
    - modification du code saisi => invalidation etat precedent
- Flutter checkout:
  - ajout section coupon (champ + bouton `Appliquer`)
  - validation via endpoint `validate-coupon`
  - feedback visuel succes/erreur + message utilisateur
  - total checkout mis a jour avec remise validee
  - coupon envoye a la commande uniquement si applique
  - changement d'adresse/zone => revalidation coupon active

## Validation executee (lot 45.9)

- `dotnet build DoorMarket.Web/DoorMarket.Web.csproj`: success
- `flutter analyze lib/core/models/cart.dart lib/core/api/cart_api.dart lib/features/checkout/checkout_screen.dart lib/features/cart/cart_screen.dart`: success

## Tests manuels recommandes (lot 45.9)

1. Web - coupon valide:
   - aller sur `/checkout`
   - saisir un coupon actif puis cliquer `Appliquer`
   - verifier message succes, remise affichee, total diminue
2. Web - coupon invalide:
   - saisir code inconnu puis `Appliquer`
   - verifier message clair et absence de remise
3. Web - effacement coupon:
   - apres coupon applique, cliquer `Effacer`
   - verifier suppression remise et retour total initial
4. Web - changement zone livraison:
   - appliquer coupon, changer zone
   - verifier revalidation et recalcul frais+total
5. Web - commande finale:
   - finaliser commande avec coupon applique
   - verifier en backend que `promoCode` est present
   - refaire sans coupon applique et verifier `promoCode` absent
6. Mobile - coupon valide:
   - ouvrir checkout mobile
   - saisir coupon puis `Appliquer`
   - verifier message succes + total mis a jour
7. Mobile - coupon invalide:
   - appliquer code invalide
   - verifier message d'echec et total inchange
8. Mobile - revalidation adresse:
   - appliquer coupon, modifier adresse/zone
   - verifier revalidation et recalcul

## Lot livre dans cette phase (45.10)

### Abandoned cart events - fondation API + anti-spam (DM45-CRT-01)

- Domain/DB:
  - nouvelle entite `AbandonedCartEvent`
  - migration EF `AddAbandonedCartEvents`
  - table `AbandonedCartEvents` avec:
    - `userId`, `cartId`
    - snapshots (`currency`, `itemCount`, `subtotal`)
    - horodatage activite (`lastCartActivityAtUtc`, `detectedAtUtc`)
    - suivi relance (`reminderStatus`, `reminderAttemptCount`, `sentChannels`, `error`, `reminderSentAtUtc`)
- Regles anti-spam:
  - fenetre configurable `CartRecovery:AntiSpamWindowMinutes`
  - pas de nouvel evenement de relance si evenement recent deja present pour le meme `cartId`+`userId`
- Regles conversion:
  - skip relance si commande creee apres la derniere activite panier

## Lot livre dans cette phase (45.11)

### Job relance panier abandonne (email/push) - scheduler basique (DM45-CRT-02)

- Service metier:
  - `IAbandonedCartRecoveryService` + implementation `AbandonedCartRecoveryService`
  - detection paniers stale selon `CartRecovery:StaleAfterMinutes`
  - creation evenement + tentative relance
- Worker background:
  - `AbandonedCartRecoveryWorker` (`BackgroundService`)
  - execution periodique selon `CartRecovery:RunIntervalMinutes`
  - logs de run (scanned/events/sent/failed/skipped)
- Canaux notification:
  - Email: envoi via `IEmailSender`
  - Push: base branchee via `ICartReminderPushSender` (noop configurable)
- Observabilite:
  - journalisation dans `TransactionalNotificationLogs`:
    - `CartAbandonedReminderEmail`
    - `CartAbandonedReminderPush`
- Configuration:
  - section `CartRecovery` ajoutee dans appsettings:
    - `Enabled`
    - `RunIntervalMinutes`
    - `StaleAfterMinutes`
    - `AntiSpamWindowMinutes`
    - `ScanBatchSize`
    - `MaxNotificationsPerRun`
    - `PushEnabled`

## Lot livre dans cette phase (45.12)

### QA/Doc complet + guide support (DM45-OBS-01)

- Document livre:
  - `docs/DM45_QA_SUPPORT_RETURNS_COUPONS_CART_RECOVERY.md`
- Contenu:
  - matrice QA returns/coupons/cart recovery
  - checks SQL operationnels
  - playbooks support par type d'incident
  - templates de reponse support

## Validation executee (lots 45.10/45.11/45.12)

- `dotnet build DoorMarket.Api/DoorMarket.Api.csproj`: success
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AbandonedCartRecoveryServiceTests"`: success
- `dotnet test DoorMarket.Tests/DoorMarket.Tests.csproj --filter "FullyQualifiedName~AbandonedCartRecoveryServiceTests|FullyQualifiedName~NotificationReplayServiceTests|FullyQualifiedName~ClientOrderNotificationServiceTests|FullyQualifiedName~AdminOperationsControllerTests"`: success

## Tests manuels recommandes (lots 45.10/45.11/45.12)

1. Detection panier abandonne:
   - creer panier avec items
   - laisser depasser `StaleAfterMinutes`
   - verifier creation row `AbandonedCartEvents`
2. Anti-spam:
   - relancer le worker dans la fenetre anti-spam
   - verifier absence de nouvel event pour meme cart/user
3. Skip conversion:
   - creer commande apres derniere activite panier
   - verifier `ConvertedSkipped` et absence de relance
4. Email relance:
   - verifier log `CartAbandonedReminderEmail` dans `TransactionalNotificationLogs`
   - verifier contenu email (resume panier + lien cart/support)
5. Push basique:
   - `PushEnabled=false`: pas de tentative push
   - `PushEnabled=true` sans provider: echec push explicite loggue
6. QA support:
   - executer checklist du guide `DM45_QA_SUPPORT_RETURNS_COUPONS_CART_RECOVERY.md`
