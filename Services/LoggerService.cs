using System;
using System.IO;

namespace LteTestStation.Services
{
    public class LoggerService
    {
        private static readonly object _fileLock = new();
        private readonly string _logDirectory;

        public event Action<string>? LogMessageReceived;

        public LoggerService()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public void Log(string message, string level = "INFO")
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[{timestamp}] [{level}] {message}";

            LogMessageReceived?.Invoke(formatted);

            try
            {
                string logFile = Path.Combine(_logDirectory, $"test_log_{DateTime.Now:yyyyMMdd}.txt");
                lock (_fileLock)
                {
                    File.AppendAllText(logFile, formatted + Environment.NewLine);
                }
            }
            catch
            {
                // Prevent crash if file is locked
            }
        }

        public void LogError(string message, Exception? ex = null)
        {
            string details = ex != null ? $"{message} | Ex: {ex.Message}" : message;
            Log(details, "ERROR");
        }

        public void LogHardwareTx(string data)
        {
            Log($"[TX] {data}", "DEBUG");
        }

        public void LogHardwareRx(string data)
        {
            Log($"[RX] {data}", "DEBUG");
        }
    }
}
