using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LteTestStation.Database;
using LteTestStation.Hardware;
using LteTestStation.Models;

namespace LteTestStation.Services
{
    public class TestEngine
    {
        private readonly LoggerService _logger;
        private readonly DatabaseManager _db;
        private CancellationTokenSource? _cts;

        public IDeviceCommunicator Communicator { get; set; }
        public List<TestItem> TestPlan { get; private set; } = new();
        public bool IsTesting { get; private set; }
        public bool StopOnFirstFailure { get; set; } = true;

        public event Action<TestItem, int>? StepStarted;
        public event Action<TestItem, int>? StepCompleted;
        public event Action<TestReportRecord>? TestFinished;
        public event Action<double>? ElapsedTimeUpdated;

        public TestEngine(IDeviceCommunicator communicator, DatabaseManager db, LoggerService logger)
        {
            Communicator = communicator;
            _db = db;
            _logger = logger;
            InitializeDefaultTestPlan();
        }

        public void InitializeDefaultTestPlan()
        {
            TestPlan = new List<TestItem>
            {
                new TestItem
                {
                    StepId = 1,
                    StepName = "COMM_CHECK",
                    Description = "Verify AT Serial/Socket communication",
                    Command = "AT",
                    ExpectedPattern = "OK"
                },
                new TestItem
                {
                    StepId = 2,
                    StepName = "READ_IMEI",
                    Description = "Read 15-digit Device IMEI",
                    Command = "AT+CGSN",
                    ExpectedPattern = @"\d{15}"
                },
                new TestItem
                {
                    StepId = 3,
                    StepName = "CHECK_SIM",
                    Description = "Verify SIM card presence and PIN status",
                    Command = "AT+CPIN?",
                    ExpectedPattern = "READY"
                },
                new TestItem
                {
                    StepId = 4,
                    StepName = "RF_SIGNAL_CSQ",
                    Description = "Measure RF Signal Strength (RSSI >= 15)",
                    Command = "AT+CSQ",
                    ExpectedPattern = @"\+CSQ:\s*(\d+)"
                },
                new TestItem
                {
                    StepId = 5,
                    StepName = "LTE_ATTACH",
                    Description = "Verify LTE Network Packet Attachment",
                    Command = "AT+CGATT?",
                    ExpectedPattern = @"\+CGATT:\s*1"
                },
                new TestItem
                {
                    StepId = 6,
                    StepName = "NETWORK_OPERATOR",
                    Description = "Query Registered Cellular Carrier",
                    Command = "AT+COPS?",
                    ExpectedPattern = @"\+COPS:"
                },
                new TestItem
                {
                    StepId = 7,
                    StepName = "SOCKET_PING",
                    Description = "Test TCP/IP Network Latency & Ping",
                    Command = "PING 8.8.8.8",
                    ExpectedPattern = "OK"
                },
                new TestItem
                {
                    StepId = 8,
                    StepName = "DATA_THROUGHPUT",
                    Description = "Measure LTE Upload/Download Data Bitrate",
                    Command = "SPEED_TEST",
                    ExpectedPattern = "OK"
                }
            };
        }

        public async Task<TestReportRecord> RunTestSequenceAsync(string serialNumber, string modelName, string stationId, string operatorId)
        {
            if (IsTesting)
            {
                throw new InvalidOperationException("Test sequence is already running.");
            }

            IsTesting = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var stopwatch = Stopwatch.StartNew();
            _logger.Log($"=========================================================");
            _logger.Log($"START TEST SEQUENCE | SN: {serialNumber} | Model: {modelName} | Op: {operatorId}");

            // Reset all test steps
            foreach (var step in TestPlan)
            {
                step.Reset();
            }

            string extractedImei = "";
            bool overallPass = true;
            string? firstFailedStep = null;
            string? errorMessage = null;

            // Connect to communicator if not connected
            if (!Communicator.IsConnected)
            {
                _logger.Log($"Connecting to hardware: {Communicator.ConnectionName}...");
                if (!Communicator.Connect())
                {
                    _logger.LogError($"Failed to connect to hardware communicator.");
                    overallPass = false;
                    firstFailedStep = "INIT_COMM";
                    errorMessage = "Cannot connect to hardware device interface.";
                }
            }

            if (overallPass)
            {
                for (int i = 0; i < TestPlan.Count; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        overallPass = false;
                        firstFailedStep = "USER_ABORT";
                        errorMessage = "Test was aborted by operator.";
                        break;
                    }

                    var step = TestPlan[i];
                    step.Status = TestStatus.RUNNING;
                    StepStarted?.Invoke(step, i);
                    _logger.Log($"[STEP {step.StepId}/{TestPlan.Count}] {step.StepName} - Command: {step.Command}");

                    var stepStopwatch = Stopwatch.StartNew();
                    try
                    {
                        string response = await Communicator.SendCommandAsync(step.Command, 3000);
                        stepStopwatch.Stop();
                        step.ExecutionTimeMs = stepStopwatch.ElapsedMilliseconds;
                        step.ActualResponse = response;

                        bool stepPass = ValidateStepResponse(step, response, out string? extractedVal, out string? stepErr);

                        if (step.StepName == "READ_IMEI" && !string.IsNullOrEmpty(extractedVal))
                        {
                            extractedImei = extractedVal;
                        }

                        if (stepPass)
                        {
                            step.Status = TestStatus.PASS;
                            _logger.Log($"  >> RESULT: PASS ({step.ExecutionTimeMs} ms)");
                        }
                        else
                        {
                            step.Status = TestStatus.FAIL;
                            step.ErrorDetail = stepErr ?? "Response did not match expected pattern.";
                            _logger.LogError($"  >> RESULT: FAIL ({step.ExecutionTimeMs} ms) - {step.ErrorDetail}");

                            overallPass = false;
                            firstFailedStep ??= step.StepName;
                            errorMessage ??= step.ErrorDetail;

                            if (StopOnFirstFailure)
                            {
                                StepCompleted?.Invoke(step, i);
                                // Mark remaining steps as skipped
                                for (int j = i + 1; j < TestPlan.Count; j++)
                                {
                                    TestPlan[j].Status = TestStatus.SKIPPED;
                                    StepCompleted?.Invoke(TestPlan[j], j);
                                }
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        stepStopwatch.Stop();
                        step.ExecutionTimeMs = stepStopwatch.ElapsedMilliseconds;
                        step.Status = TestStatus.FAIL;
                        step.ErrorDetail = ex.Message;
                        _logger.LogError($"  >> EXCEPTION in {step.StepName}: {ex.Message}");

                        overallPass = false;
                        firstFailedStep ??= step.StepName;
                        errorMessage ??= ex.Message;

                        StepCompleted?.Invoke(step, i);
                        if (StopOnFirstFailure) break;
                    }

                    StepCompleted?.Invoke(step, i);
                    ElapsedTimeUpdated?.Invoke(stopwatch.Elapsed.TotalSeconds);
                }
            }

            stopwatch.Stop();
            double totalSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 2);

            var report = new TestReportRecord
            {
                SerialNumber = serialNumber,
                Imei = extractedImei,
                ModelName = modelName,
                StationId = stationId,
                OperatorId = operatorId,
                OverallResult = overallPass ? "PASS" : "FAIL",
                CycleTimeSeconds = totalSeconds,
                FailedStepName = firstFailedStep,
                ErrorMessage = errorMessage,
                TestTimestamp = DateTime.Now
            };

            _logger.Log($"TEST COMPLETED: {report.OverallResult} | Cycle Time: {report.CycleTimeSeconds}s");
            _logger.Log($"=========================================================\n");

            // Save record to MSSQL Database asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    bool saved = await _db.InsertTestRecordAsync(report);
                    if (saved)
                    {
                        _logger.Log($"[DATABASE] Test record successfully saved to MSSQL (SN: {report.SerialNumber})");
                    }
                    else
                    {
                        _logger.LogError($"[DATABASE] Failed to save record to MSSQL. Logged locally. Error: {_db.LastError}");
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError($"[DATABASE] Error saving record: {dbEx.Message}");
                }
            });

            IsTesting = false;
            TestFinished?.Invoke(report);
            return report;
        }

        private bool ValidateStepResponse(TestItem step, string response, out string? extractedValue, out string? errorReason)
        {
            extractedValue = null;
            errorReason = null;

            if (string.IsNullOrWhiteSpace(response))
            {
                errorReason = "No response from device (Timeout).";
                return false;
            }

            if (response.Contains("ERROR"))
            {
                errorReason = $"Device returned error: {response}";
                return false;
            }

            switch (step.StepName)
            {
                case "COMM_CHECK":
                    return response.Contains("OK");

                case "READ_IMEI":
                    var imeiMatch = Regex.Match(response, @"\b(\d{15})\b");
                    if (imeiMatch.Success)
                    {
                        extractedValue = imeiMatch.Value;
                        return true;
                    }
                    errorReason = "Valid 15-digit IMEI not found in response.";
                    return false;

                case "CHECK_SIM":
                    if (response.Contains("READY"))
                    {
                        return true;
                    }
                    errorReason = "SIM card is not in READY state (missing or PIN locked).";
                    return false;

                case "RF_SIGNAL_CSQ":
                    var csqMatch = Regex.Match(response, @"\+CSQ:\s*(\d+)");
                    if (csqMatch.Success && int.TryParse(csqMatch.Groups[1].Value, out int rssi))
                    {
                        if (rssi >= 15) // Factory standard threshold: >= 15
                        {
                            return true;
                        }
                        errorReason = $"RSSI too low: {rssi} (Threshold required >= 15).";
                        return false;
                    }
                    errorReason = "Could not parse CSQ RSSI value.";
                    return false;

                case "LTE_ATTACH":
                    if (response.Contains("+CGATT: 1") || response.Contains("1"))
                    {
                        return true;
                    }
                    errorReason = "Device not attached to cellular packet service (+CGATT: 0).";
                    return false;

                case "NETWORK_OPERATOR":
                    return response.Contains("+COPS:") && response.Contains("OK");

                case "SOCKET_PING":
                case "DATA_THROUGHPUT":
                    return response.Contains("OK") || response.Contains("PASS");

                default:
                    return response.Contains(step.ExpectedPattern);
            }
        }

        public void AbortTest()
        {
            if (IsTesting && _cts != null)
            {
                _cts.Cancel();
                _logger.Log("[ABORT] Test sequence cancellation requested by operator.", "WARN");
            }
        }
    }
}
