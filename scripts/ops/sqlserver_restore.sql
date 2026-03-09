-- Usage:
-- sqlcmd -S <server> -d master -U <user> -P <password> -v SOURCE_BAK="D:\Backups\DoorMarketDb_full.bak" TARGET_DB="DoorMarketDb_RestoreTest" DATA_FILE="D:\Data\DoorMarketDb_RestoreTest.mdf" LOG_FILE="D:\Data\DoorMarketDb_RestoreTest_log.ldf" -i scripts/ops/sqlserver_restore.sql

DECLARE @sourceBak nvarchar(4000) = N'$(SOURCE_BAK)';
DECLARE @targetDb sysname = N'$(TARGET_DB)';
DECLARE @dataFile nvarchar(4000) = N'$(DATA_FILE)';
DECLARE @logFile nvarchar(4000) = N'$(LOG_FILE)';

IF DB_ID(@targetDb) IS NOT NULL
BEGIN
    PRINT 'Dropping existing database ' + @targetDb;
    EXEC(N'ALTER DATABASE [' + @targetDb + N'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;');
    EXEC(N'DROP DATABASE [' + @targetDb + N'];');
END

DECLARE @fileList TABLE (
    LogicalName nvarchar(128),
    PhysicalName nvarchar(260),
    [Type] char(1),
    FileGroupName nvarchar(128),
    [Size] numeric(20,0),
    MaxSize numeric(20,0),
    FileId bigint,
    CreateLSN numeric(25,0),
    DropLSN numeric(25,0),
    UniqueId uniqueidentifier,
    ReadOnlyLSN numeric(25,0),
    ReadWriteLSN numeric(25,0),
    BackupSizeInBytes bigint,
    SourceBlockSize int,
    FileGroupId int,
    LogGroupGUID uniqueidentifier,
    DifferentialBaseLSN numeric(25,0),
    DifferentialBaseGUID uniqueidentifier,
    IsReadOnly bit,
    IsPresent bit,
    TDEThumbprint varbinary(32),
    SnapshotURL nvarchar(360)
);

DECLARE @fileListSql nvarchar(max) = N'RESTORE FILELISTONLY FROM DISK = N''' + @sourceBak + N''';';
INSERT INTO @fileList
EXEC sp_executesql @fileListSql;

DECLARE @logicalData nvarchar(128) = (SELECT TOP 1 LogicalName FROM @fileList WHERE [Type] = 'D');
DECLARE @logicalLog nvarchar(128) = (SELECT TOP 1 LogicalName FROM @fileList WHERE [Type] = 'L');

DECLARE @restoreSql nvarchar(max) = N'
RESTORE DATABASE [' + @targetDb + N']
FROM DISK = N''' + @sourceBak + N'''
WITH MOVE N''' + @logicalData + N''' TO N''' + @dataFile + N''',
     MOVE N''' + @logicalLog + N''' TO N''' + @logFile + N''',
     REPLACE, RECOVERY, STATS = 10;';

PRINT 'Starting restore to ' + @targetDb;
EXEC sp_executesql @restoreSql;

PRINT 'Restore completed.';
