# DM60-OPS-02 - Backup/Restore teste + RTO/RPO documentes

Date: 2026-03-06

## Artefacts implementes

- Scripts SQL:
  - `scripts/ops/sqlserver_backup.sql`
  - `scripts/ops/sqlserver_restore.sql`
  - `scripts/ops/sqlserver_restore_smoke.sql`

## Procedure de test backup/restore (SQL Server)

### 1) Backup full
```powershell
sqlcmd -S <server> -d master -U <user> -P <password> `
  -v DB_NAME="DoorMarketDb" BACKUP_FILE="D:\Backups\DoorMarketDb_full.bak" `
  -i scripts/ops/sqlserver_backup.sql
```

### 2) Restore sur base de test
```powershell
sqlcmd -S <server> -d master -U <user> -P <password> `
  -v SOURCE_BAK="D:\Backups\DoorMarketDb_full.bak" `
     TARGET_DB="DoorMarketDb_RestoreTest" `
     DATA_FILE="D:\Data\DoorMarketDb_RestoreTest.mdf" `
     LOG_FILE="D:\Data\DoorMarketDb_RestoreTest_log.ldf" `
  -i scripts/ops/sqlserver_restore.sql
```

### 3) Smoke post-restore
```powershell
sqlcmd -S <server> -d DoorMarketDb_RestoreTest -U <user> -P <password> `
  -i scripts/ops/sqlserver_restore_smoke.sql
```

## RTO / RPO cibles

- RPO cible: 15 minutes (backup incrementaux/log backup)
- RTO cible: 60 minutes (restore + verification smoke + switch)

## Resultats a capturer apres test

Completer ce tableau a chaque exercice:

| Date UTC | Backup start | Backup end | Restore start | Restore end | RPO observe | RTO observe | Resultat |
|---|---|---|---|---|---|---|---|
| YYYY-MM-DD HH:mm | | | | | | | PASS/FAIL |

## Frequence recommandee

- Backup full: quotidien
- Backup diff/log: toutes les 15 min a 60 min selon offre hebergeur
- Test restore: hebdomadaire (minimum mensuel en production)

## Notes SmarterASP

Si `BACKUP DATABASE` n’est pas autorise par l’hebergeur:
- utiliser export logique (BACPAC/outil hebergeur),
- tester import sur base clone,
- garder la meme grille RTO/RPO.
