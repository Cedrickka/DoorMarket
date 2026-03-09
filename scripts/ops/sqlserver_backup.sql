-- Usage:
-- sqlcmd -S <server> -d master -U <user> -P <password> -v DB_NAME="DoorMarketDb" BACKUP_FILE="D:\Backups\DoorMarketDb_full.bak" -i scripts/ops/sqlserver_backup.sql

DECLARE @db sysname = N'$(DB_NAME)';
DECLARE @backupFile nvarchar(4000) = N'$(BACKUP_FILE)';

PRINT 'Starting full backup for ' + @db + ' -> ' + @backupFile;

DECLARE @sql nvarchar(max) = N'
BACKUP DATABASE [' + @db + N']
TO DISK = N''' + @backupFile + N'''
WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;';

EXEC sp_executesql @sql;

PRINT 'Backup completed.';
