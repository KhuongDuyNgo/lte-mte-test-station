using System;

namespace LteTestStation.Models
{
    public class TestReportRecord
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Imei { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public string OperatorId { get; set; } = string.Empty;
        public string OverallResult { get; set; } = "FAIL"; // PASS or FAIL
        public double CycleTimeSeconds { get; set; }
        public string? FailedStepName { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime TestTimestamp { get; set; } = DateTime.Now;
    }

    public class YieldStatistics
    {
        public int TotalTested { get; set; }
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public double YieldRatePercent => TotalTested > 0 ? Math.Round((double)PassedCount / TotalTested * 100.0, 2) : 0.0;
        public double AverageCycleTimeSeconds { get; set; }
    }
}
