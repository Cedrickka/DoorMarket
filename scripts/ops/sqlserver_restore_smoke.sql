-- Usage:
-- sqlcmd -S <server> -d <restored_db> -U <user> -P <password> -i scripts/ops/sqlserver_restore_smoke.sql

SET NOCOUNT ON;

SELECT DB_NAME() AS CurrentDatabase, SYSUTCDATETIME() AS CheckedAtUtc;

SELECT TOP (1) Id, Email, Role, CreatedAtUtc
FROM Users
ORDER BY CreatedAtUtc DESC;

SELECT TOP (5) Id, Status, PaymentStatus, CreatedAtUtc
FROM Orders
ORDER BY CreatedAtUtc DESC;

SELECT TOP (5) Id, Status, CreatedAtUtc
FROM ShopPayouts
ORDER BY CreatedAtUtc DESC;
