-- ==========================================================
-- LTE Device Automated Test Station - MSSQL Database Script
-- Database: LteTestStationDb
-- Author: MTE Engineer
-- ==========================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'LteTestStationDb')
BEGIN
    CREATE DATABASE [LteTestStationDb];
    PRINT 'Database [LteTestStationDb] created successfully.';
END
GO

USE [LteTestStationDb];
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TestRecords]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TestRecords] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [SerialNumber] NVARCHAR(64) NOT NULL,
        [Imei] NVARCHAR(32) NULL,
        [ModelName] NVARCHAR(64) NOT NULL,
        [StationId] NVARCHAR(32) NOT NULL,
        [OperatorId] NVARCHAR(32) NOT NULL,
        [OverallResult] NVARCHAR(10) NOT NULL, -- 'PASS' or 'FAIL'
        [CycleTimeSeconds] FLOAT NOT NULL,
        [FailedStepName] NVARCHAR(128) NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [TestTimestamp] DATETIME NOT NULL DEFAULT (GETDATE())
    );

    CREATE NONCLUSTERED INDEX [IX_TestRecords_SN] ON [dbo].[TestRecords]([SerialNumber]);
    CREATE NONCLUSTERED INDEX [IX_TestRecords_Timestamp] ON [dbo].[TestRecords]([TestTimestamp]);
    CREATE NONCLUSTERED INDEX [IX_TestRecords_Result] ON [dbo].[TestRecords]([OverallResult]);

    PRINT 'Table [TestRecords] created successfully with indexes.';
END
GO

-- Seed sample production data for Weekly Review Demonstration
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[TestRecords])
BEGIN
    INSERT INTO [dbo].[TestRecords] 
        ([SerialNumber], [Imei], [ModelName], [StationId], [OperatorId], [OverallResult], [CycleTimeSeconds], [FailedStepName], [ErrorMessage], [TestTimestamp])
    VALUES
        ('SN-LTE2026-001', '865492040188211', 'LTE-ROUTER-V2', 'MTE-STATION-01', 'OP-882', 'PASS', 4.25, NULL, NULL, DATEADD(hour, -5, GETDATE())),
        ('SN-LTE2026-002', '865492040188212', 'LTE-ROUTER-V2', 'MTE-STATION-01', 'OP-882', 'PASS', 4.10, NULL, NULL, DATEADD(hour, -4, GETDATE())),
        ('SN-LTE2026-003', '865492040188213', 'LTE-ROUTER-V2', 'MTE-STATION-01', 'OP-882', 'FAIL', 2.30, 'RF_SIGNAL_CSQ', 'Low RSSI (CSQ: 6 < threshold 15)', DATEADD(hour, -3, GETDATE())),
        ('SN-LTE2026-004', '865492040188214', 'LTE-ROUTER-V2', 'MTE-STATION-01', 'OP-882', 'PASS', 4.18, NULL, NULL, DATEADD(hour, -2, GETDATE())),
        ('SN-LTE2026-005', '865492040188215', 'LTE-ROUTER-V2', 'MTE-STATION-01', 'OP-882', 'PASS', 4.05, NULL, NULL, DATEADD(hour, -1, GETDATE()));
    PRINT 'Sample test data inserted.';
END
GO

-- Sample Weekly Yield Query (Used in Weekly Report Review)
SELECT 
    COUNT(*) AS TotalTested,
    SUM(CASE WHEN OverallResult = 'PASS' THEN 1 ELSE 0 END) AS TotalPassed,
    SUM(CASE WHEN OverallResult = 'FAIL' THEN 1 ELSE 0 END) AS TotalFailed,
    ROUND(CAST(SUM(CASE WHEN OverallResult = 'PASS' THEN 1 ELSE 0 END) AS FLOAT) / COUNT(*) * 100.0, 2) AS YieldRatePercent,
    ROUND(AVG(CycleTimeSeconds), 2) AS AvgCycleTimeSeconds
FROM [dbo].[TestRecords]
WHERE [TestTimestamp] >= DATEADD(day, -7, GETDATE());
GO
