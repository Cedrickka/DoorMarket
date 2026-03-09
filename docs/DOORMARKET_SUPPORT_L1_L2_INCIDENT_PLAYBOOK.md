# DoorMarket - Support Playbook L1/L2 (Mobile + Web)

## 1. Objectif
Ce guide donne une procedure standard pour le support niveau 1 et niveau 2.
- L1: qualification, verification rapide, mitigation utilisateur.
- L2: diagnostic technique, correctif, validation et communication de cloture.

## 2. Regles de triage
Toujours collecter avant action:
1. Date/heure UTC precise.
2. Plateforme (Android/iOS/Web), version app, version backend.
3. Ecran exact + etapes de reproduction.
4. Identifiants utiles (userId, orderId, cartId, requestId).
5. Capture ecran + logs (Flutter console / API logs / navigateur).

Severite:
- SEV1: checkout/paiement bloque pour plusieurs clients.
- SEV2: fonctionnalite majeure degradee (search, notifications, adresses).
- SEV3: bug visuel ou cas limite sans blocage transactionnel.

## 3. Incidents - runbooks details

### INC-01 - Home mobile vide + erreurs `semantics.parentDataDirty`
Symptome:
- Home n affiche plus les sections.
- Console Flutter spamme `Failed assertion: !semantics.parentDataDirty`.

L1 actions:
1. Fermer/reouvrir l app.
2. Faire un hard restart (`flutter clean` local dev, sinon reinstall app test).
3. Verifier si le bug arrive sur un seul ecran ou plusieurs.
4. Escalader L2 si reproductible 2 fois de suite.

L2 diagnostic:
1. Chercher un widget reconstruit avec contraintes invalides (Nested scroll/list/grid).
2. Verifier les childrens dynamiques dans Sliver/ListView/GridView.
3. Verifier qu aucun widget n est reparentage dans 2 branches simultanees.
4. Lancer `flutter analyze` puis profil rendering.

Resolution attendue:
- Stabiliser la structure parent/enfant.
- Eviter les changements de layout pendant frame active.

### INC-02 - Notification refresh casse layout (`child.hasSize`, `infinite width`)
Symptome:
- Apres pull-to-refresh dans Notifications: erreurs sliver + ecran casse.

L1 actions:
1. Capturer l etat (filtres actifs, unread only, actionable only).
2. Reproduire sur connexion normale puis connexion lente.
3. Verifier si l erreur disparait apres reouverture ecran.

L2 diagnostic:
1. Valider contraintes des widgets dans cards/rows/expanded.
2. Supprimer tout widget qui impose largeur infinie dans item list.
3. Controler les donnees nullables avant rendu.
4. Rejouer sur dataset vide + dataset charge.

Resolution attendue:
- Aucun assert layout en refresh.
- Ecran stable en etat vide, erreur, charge.

### INC-03 - Formulaire adresse: erreur zone livraison (Dropdown)
Symptome:
- Erreur rouge lors de selection zone.
- Assert Flutter: "There should be exactly one item with DropdownButton value".

L1 actions:
1. Demander pays/ville/quartier saisis.
2. Verifier si la zone existe en base pour la ville.
3. Tester creation adresse avec une autre zone.

L2 diagnostic:
1. Dedoublonner les options zone par `id`.
2. Verifier que la valeur selectionnee existe dans la liste courante.
3. Rejeter les payloads incomplets avec message API lisible.

Resolution attendue:
- Dropdown stable, aucune valeur dupliquee.
- Message utilisateur clair en cas de zone invalide.

### INC-04 - Recherche mobile renvoie ecran vide
Symptome:
- Recherche lancee, page vide ou resultat absent sans message.

L1 actions:
1. Tester une requete simple connue (ex: "pizza").
2. Verifier API `/api/search/*` repond (code 200).
3. Verifier filtres actifs (prix, stock, categorie).

L2 diagnostic:
1. Verifier binding query params mobile -> API.
2. Verifier gestion `empty/error/loading` cote UI.
3. Verifier ranking/filtres ne filtrent pas tout par defaut.

Resolution attendue:
- Resultats affiches ou message "Aucun resultat" explicite.

### INC-05 - Panier mobile overflow / cartes coupees
Symptome:
- `BOTTOM OVERFLOWED BY ... PIXELS` sur lignes panier.

L1 actions:
1. Capturer resolution ecran, langue active, taille police systeme.
2. Reproduire avec noms produits longs et quantite > 1.

L2 diagnostic:
1. Ajouter `Expanded/Flexible` sur colonnes texte.
2. Reduire paddings horizontaux, contraintes min/max.
3. Limiter a `maxLines` + ellipsis pour labels longs.

Resolution attendue:
- Aucun overflow en tailles ecran courantes.

### INC-06 - `multiple heroes share the same tag`
Symptome:
- Navigation vers detail produit echoue avec erreur Hero.

L1 actions:
1. Identifier ecran source/destination.
2. Fournir l id produit exact.

L2 diagnostic:
1. Garantir un `heroTag` unique par item.
2. Desactiver Hero sur boutons utilitaires non necessaires.

Resolution attendue:
- Transition detail stable, sans collision de tag.

### INC-07 - Push non actif (Firebase options introuvables)
Symptome:
- Log: `Firebase initialize skipped` / `Failed to load FirebaseOptions`.

L1 actions:
1. Informer client: notifications push temporairement indisponibles.
2. Verifier version app et build flavor.

L2 diagnostic:
1. Verifier `google-services.json` (Android) et plist iOS.
2. Verifier valeurs Firebase dans resources pour le flavor actif.
3. Verifier token device enregistre cote API.

Resolution attendue:
- Initialisation Firebase OK.
- Token push remonte et visible en logs backend.

### INC-08 - Web media proxy: `Response ended prematurely`
Symptome:
- Web affiche erreurs HttpIOException lors chargement images.

L1 actions:
1. Verifier URL image directe accessible dans navigateur.
2. Recharger page et noter si image specifique ou globale.

L2 diagnostic:
1. Verifier timeout/retry `HttpClient MediaProxy`.
2. Verifier upstream `api.door-market.com/uploads` (latence, reset).
3. Ajouter fallback image en UI si stream coupe.

Resolution attendue:
- Image chargee ou fallback sans exception bloquante.

### INC-09 - Menu web icones remplaces par points
Symptome:
- Nav menu affiche `.` a la place des icones.

L1 actions:
1. Forcer refresh cache navigateur (Ctrl+F5).
2. Verifier si bug present sur autre navigateur.

L2 diagnostic:
1. Verifier police/icones statiques bien servies (MIME, chemin, version).
2. Verifier package icon font reference cote layout.
3. Rebuild front avec purge cache CDN.

Resolution attendue:
- Icones correctes sur tous navigateurs supportes.

### INC-10 - API 400 LINQ non traduisible / migration FK cascade
Symptome:
- Reconciliation renvoie 400 avec expression LINQ non traduisible.
- Migration SQL echoue: multiple cascade paths.

L1 actions:
1. Capturer endpoint exact + payload + plage de date.
2. Confirmer environnement impacte (dev/staging/prod).

L2 diagnostic:
1. LINQ: sortir aggregation complexe vers projection SQL-compatible.
2. Migration: remplacer cascade FK conflictuelle par `NoAction` ou `SetNull`.
3. Regenerer migration, tester base vide et base existante.

Resolution attendue:
- Endpoint reconciliation retourne 200.
- `dotnet ef database update` passe sans erreur FK.

## 4. Escalade L1 -> L2
Escalader immediatement si:
- incident paiement/checkout (SEV1),
- perte de donnees,
- plantage reproductible sur ecran principal,
- erreur backend 5xx persistante > 10 minutes.

Template escalation:
- Incident ID:
- Severite:
- Plateforme/version:
- Reproduction (steps):
- Logs (extrait):
- Impact client:
- Action L1 deja faite:

## 5. Verification de cloture
L2 cloture seulement si:
1. Correctif applique et revu.
2. Verification manuelle passee (happy path + regression rapide).
3. Monitoring 24h sans recurrence.
4. Note de release/documentation mise a jour.
