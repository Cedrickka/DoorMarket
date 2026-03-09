# DM60-QA-03 - Non-regression reconciliation / commissions / notifications

Date: 2026-03-06

## Script automatise

- `scripts/qa/run_dm60_nonreg_recon_comm_notif.ps1`

Execution:
```powershell
./scripts/qa/run_dm60_nonreg_recon_comm_notif.ps1 -Configuration Release
```

Suites cibles:
- `AdminReconciliationControllerTests`
- `AdminCommissionsControllerTests`
- `AdminNotificationsControllerTests`
- `NotificationReplayServiceTests`
- `ClientOrderNotificationServiceTests`

## Verification manuelle complementaire

1. Reconciliation
- charger tableau payouts
- verifier synthese devise
- verifier pas d’erreur LINQ 400

2. Commissions
- verifier affichage regles
- simuler preview frais produit
- verifier application regle active

3. Notifications
- creer evenement commande
- verifier envoi client/admin
- verifier replay notification

## Evidence attendue

- `trx` dans `artifacts/qa`
- logs API sans erreur 400/500 sur ces modules
- captures ecran admin (recon, commissions, notifications)
