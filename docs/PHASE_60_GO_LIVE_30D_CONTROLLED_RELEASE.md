# Phase 60 - Go-Live controle 30 jours (Web + Mobile + API)

## Objectif
Mettre DoorMarket en production de facon controlee, avec un risque acceptable pour un lancement MVP reel.

Important:
- Cette phase ne vise pas la parite Amazon/Alibaba.
- Elle vise un go-live fiable et exploitable avec monitoring, process support, securite et paiements stables.

## Positionnement realiste
- Go-live MVP controle: `possible` si les gates ci-dessous sont valides.
- Niveau marketplace global type Amazon/Alibaba: `non atteint` a ce stade.

## Gates Go/No-Go (bloquants)
Chaque gate est `PASS` ou `FAIL`.

1. Disponibilite et stabilite
- API p95 < 900 ms sur endpoints critiques (`search`, `cart`, `checkout`, `orders`).
- Taux erreur 5xx < 1% en charge nominale.
- Aucun crash bloquant web/mobile durant 48h de smoke continu.

2. Checkout et paiement
- Parcours checkout passe pour tous providers actifs.
- Reprise paiement post-echec validee (web + mobile).
- Reconciliation quotidienne sans ecart critique non explique.

3. Securite minimale
- Secrets hors code source.
- TLS + headers de securite actifs.
- Journalisation des actions sensibles admin.
- Rate-limit actif sur auth/search/checkout.

4. Support et exploitation
- Runbook L1/L2 disponible et teste.
- Escalade incident (on-call) definie.
- Tableau de bord production + alertes (SLO, paiement, jobs).

5. Qualite fonctionnelle
- Regression manuelle complete passee (web/mobile/api).
- Aucun bug `P0/P1` ouvert sur flux: inscription, catalogue, panier, checkout, commandes.

## Plan 30 jours (sequences)

### Semaine 1 - Stabilisation critique
- Fix P0/P1 ouverts checkout/cart/search.
- Hardening logs/erreurs API.
- Validation complete parcours paiement.

### Semaine 2 - Observabilite et support
- Dashboards prod (latence, erreurs, paiements, jobs).
- Alertes + seuils + escalade.
- Dry-run support L1/L2 sur incidents types.

### Semaine 3 - Charge et securite
- Tests charge baseline + tuning p95/p99.
- Revue securite OWASP base + durcissement.
- Verification sauvegardes + procedure restauration.

### Semaine 4 - Lancement controle
- Soft launch (cohorte limitee).
- Monitoring renforce 72h.
- Decision Go/No-Go finale.

## Checklist execution (DoD)

### Produit
- [ ] Flux client de bout en bout OK (web/mobile).
- [ ] Etats vides/erreurs traduits FR/EN et explicites.
- [ ] Accessibilite de base (focus clavier, contrastes critiques).

### Tech
- [ ] Migration DB rejouee sur env preprod sans erreur.
- [ ] Jobs planifies monitorables (retry, dead-letter, logs).
- [ ] Sauvegarde/restauration valides (testees).

### Operations
- [ ] Incidents classes par severite (SEV1..SEV3).
- [ ] SLO publies et suivis.
- [ ] Playbook paiement/reconciliation execute au moins 1 fois en simulation.

## Indicateurs cibles de lancement
- Conversion checkout submit -> paid: >= 75% (MVP cible).
- Echecs paiement techniques: <= 5%.
- Temps reponse p95 endpoints critiques: <= 900 ms.
- Taux tickets support critiques (P1): < 2% des commandes/jour.

## Risques restants acceptes (MVP)
- Couverture anti-fraude avancee limitee.
- Recommandation personnalisee encore v1.
- Capacite de charge non encore dimensionnee niveau mega-scale.

## Decision cadre
- Go si: tous gates bloquants = PASS.
- No-Go si: au moins 1 gate bloquant = FAIL.

## Livrables DM60 associes
- Support incident drill L1/L2:
  - `docs/DM60_SUP_01_SUPPORT_INCIDENT_SIMULATION.md`
  - `scripts/support/run_dm60_support_incident_drill.ps1`
- Dossier Go/No-Go:
  - `docs/DM60_DOC_01_GO_NO_GO_DOSSIER.md`
