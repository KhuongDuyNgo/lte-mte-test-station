namespace LteTestStation.Models
{
    public enum TestStatus
    {
        WAITING,
        RUNNING,
        PASS,
        FAIL,
        SKIPPED
    }

    public class TestItem
    {
        public int StepId { get; set; }
        public string StepName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string ExpectedPattern { get; set; } = string.Empty;
        public string ActualResponse { get; set; } = string.Empty;
        public TestStatus Status { get; set; } = TestStatus.WAITING;
        public long ExecutionTimeMs { get; set; }
        public string ErrorDetail { get; set; } = string.Empty;

        public void Reset()
        {
            Status = TestStatus.WAITING;
            ActualResponse = string.Empty;
            ExecutionTimeMs = 0;
            ErrorDetail = string.Empty;
        }
    }
}
