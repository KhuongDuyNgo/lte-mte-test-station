using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using LteTestStation.Models;

namespace LteTestStation.Database
{
    public class DatabaseManager
    {
        public string ConnectionString { get; set; } = 
            "Server=localhost;Database=LteTestStationDb;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=3;";

        public bool IsConnected { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        public DatabaseManager(string? connectionString = null)
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                ConnectionString = connectionString;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();
                IsConnected = true;
                LastError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                LastError = ex.Message;
                return false;
            }
        }

        public async Task<bool> EnsureDatabaseAndTableAsync()
        {
            try
            {
                // Try connecting and creating table if not exists
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();

                string createTableSql = @"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TestRecords]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [dbo].[TestRecords] (
                        [Id] INT IDENTITY(1,1) PRIMARY KEY,
                        [SerialNumber] NVARCHAR(64) NOT NULL,
                        [Imei] NVARCHAR(32) NULL,
                        [ModelName] NVARCHAR(64) NOT NULL,
                        [StationId] NVARCHAR(32) NOT NULL,
                        [OperatorId] NVARCHAR(32) NOT NULL,
                        [OverallResult] NVARCHAR(10) NOT NULL,
                        [CycleTimeSeconds] FLOAT NOT NULL,
                        [FailedStepName] NVARCHAR(128) NULL,
                        [ErrorMessage] NVARCHAR(MAX) NULL,
                        [TestTimestamp] DATETIME NOT NULL DEFAULT (GETDATE())
                    );
                END";

                using var cmd = new SqlCommand(createTableSql, conn);
                await cmd.ExecuteNonQueryAsync();
                IsConnected = true;
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                LastError = ex.Message;
                return false;
            }
        }

        public async Task<bool> InsertTestRecordAsync(TestReportRecord record)
        {
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();

                string sql = @"
                INSERT INTO [dbo].[TestRecords] 
                    ([SerialNumber], [Imei], [ModelName], [StationId], [OperatorId], 
                     [OverallResult], [CycleTimeSeconds], [FailedStepName], [ErrorMessage], [TestTimestamp])
                VALUES 
                    (@SerialNumber, @Imei, @ModelName, @StationId, @OperatorId, 
                     @OverallResult, @CycleTimeSeconds, @FailedStepName, @ErrorMessage, @TestTimestamp);";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@SerialNumber", record.SerialNumber);
                cmd.Parameters.AddWithValue("@Imei", (object?)record.Imei ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ModelName", record.ModelName);
                cmd.Parameters.AddWithValue("@StationId", record.StationId);
                cmd.Parameters.AddWithValue("@OperatorId", record.OperatorId);
                cmd.Parameters.AddWithValue("@OverallResult", record.OverallResult);
                cmd.Parameters.AddWithValue("@CycleTimeSeconds", record.CycleTimeSeconds);
                cmd.Parameters.AddWithValue("@FailedStepName", (object?)record.FailedStepName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ErrorMessage", (object?)record.ErrorMessage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TestTimestamp", record.TestTimestamp);

                await cmd.ExecuteNonQueryAsync();
                IsConnected = true;
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                LastError = ex.Message;
                return false;
            }
        }

        public async Task<List<TestReportRecord>> GetRecentRecordsAsync(int top = 50)
        {
            var list = new List<TestReportRecord>();
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();

                string sql = $@"
                SELECT TOP ({top}) [Id], [SerialNumber], [Imei], [ModelName], [StationId], 
                       [OperatorId], [OverallResult], [CycleTimeSeconds], [FailedStepName], 
                       [ErrorMessage], [TestTimestamp]
                FROM [dbo].[TestRecords]
                ORDER BY [TestTimestamp] DESC;";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new TestReportRecord
                    {
                        Id = reader.GetInt32(0),
                        SerialNumber = reader.GetString(1),
                        Imei = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        ModelName = reader.GetString(3),
                        StationId = reader.GetString(4),
                        OperatorId = reader.GetString(5),
                        OverallResult = reader.GetString(6),
                        CycleTimeSeconds = reader.GetDouble(7),
                        FailedStepName = reader.IsDBNull(8) ? null : reader.GetString(8),
                        ErrorMessage = reader.IsDBNull(9) ? null : reader.GetString(9),
                        TestTimestamp = reader.GetDateTime(10)
                    });
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            return list;
        }

        public async Task<YieldStatistics> GetYieldStatisticsAsync(DateTime fromDate, DateTime toDate)
        {
            var stats = new YieldStatistics();
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();

                string sql = @"
                SELECT 
                    COUNT(*) AS TotalTested,
                    ISNULL(SUM(CASE WHEN OverallResult = 'PASS' THEN 1 ELSE 0 END), 0) AS TotalPassed,
                    ISNULL(SUM(CASE WHEN OverallResult = 'FAIL' THEN 1 ELSE 0 END), 0) AS TotalFailed,
                    ISNULL(AVG(CycleTimeSeconds), 0) AS AvgCycleTime
                FROM [dbo].[TestRecords]
                WHERE [TestTimestamp] >= @FromDate AND [TestTimestamp] <= @ToDate;";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@FromDate", fromDate);
                cmd.Parameters.AddWithValue("@ToDate", toDate);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    stats.TotalTested = reader.GetInt32(0);
                    stats.PassedCount = reader.GetInt32(1);
                    stats.FailedCount = reader.GetInt32(2);
                    stats.AverageCycleTimeSeconds = Math.Round(reader.GetDouble(3), 2);
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            return stats;
        }

        public async Task<Dictionary<string, int>> GetFailureParetoAsync(DateTime fromDate, DateTime toDate)
        {
            var pareto = new Dictionary<string, int>();
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();

                string sql = @"
                SELECT [FailedStepName], COUNT(*) AS FailureCount
                FROM [dbo].[TestRecords]
                WHERE [OverallResult] = 'FAIL' 
                  AND [FailedStepName] IS NOT NULL
                  AND [TestTimestamp] >= @FromDate AND [TestTimestamp] <= @ToDate
                GROUP BY [FailedStepName]
                ORDER BY FailureCount DESC;";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@FromDate", fromDate);
                cmd.Parameters.AddWithValue("@ToDate", toDate);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string step = reader.GetString(0);
                    int count = reader.GetInt32(1);
                    pareto[step] = count;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            return pareto;
        }
    }
}
