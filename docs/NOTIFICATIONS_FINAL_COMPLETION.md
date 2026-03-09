# Notifications - Cloture Finale (Web + Mobile + API)

## Scope final livre

### Backend/API
- Notifications transactionnelles (commande creee, paiement paye/echoue).
- Observabilite:
  - logs persistants des tentatives.
  - vues admin filtrables + summary.
- Operations:
  - retry unitaire.
  - retry bulk unresolved.
  - incidents unresolved agreges.
  - export CSV.
  - acknowledge / reopen incidents.

### Web Admin
- Centre notifications admin complet (`/admin/notifications`):
  - filtres, summary, incidents, transactions.
  - retry unitaire + bulk.
  - export CSV.
  - gestion incidents acknowledge/reopen.

### Mobile Flutter
- Vue client notifications dynamique (basee commandes).
- Vue admin notifications operationnelle.
- Preferences persistantes:
  - activation globale + categories.
- Lu/non-lu:
  - unread-only, mark all read, reset historique.
- Badges non lus partages:
  - Home + Profile.
- Mode triage:
  - ouverture directe en non-lu, filtres actionnables/categories, priorisation.
- Synchronisation:
  - auto-refresh periodique (45s),
  - guard stale + refresh manuel.
- Resilience:
  - cache offline feed + fallback compteur non lu.

## Verification technique finale
- Backend tests (deja executes sur phases API): OK.
- Web build/tests ciblés (phases 19-22): OK.
- Flutter:
  - `flutter analyze`: OK
  - `flutter test`: OK (inclut tests unitaires notifications/cache/preferences)

## Tests d'acceptance metier (final)
1. Admin: creer un echec notification, verifier incident unresolved.
2. Admin: retry incident, verifier nouvelle tentative log.
3. Admin: acknowledge puis reopen, verifier comportement attendu.
4. Web Admin: export CSV filtre, verifier contenu.
5. Mobile client: verifier feed, triage, mark read, unread-only.
6. Mobile client: verifier badges Home/Profile avant/apres lecture.
7. Mobile offline: couper reseau, verifier fallback cache.
8. Mobile settings: desactiver categories, verifier filtrage feed + badges.

## Statut
La partie notifications est consideree **terminee** sur le perimetre actuel.
Les prochaines evolutions peuvent se concentrer sur d'autres domaines (catalogue, checkout, reconciliation, marketing), sans blocage notifications.
