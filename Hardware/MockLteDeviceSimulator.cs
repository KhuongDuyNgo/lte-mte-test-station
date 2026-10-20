using System;
using System.Threading.Tasks;

namespace LteTestStation.Hardware
{
    public class MockLteDeviceSimulator : IDeviceCommunicator
    {
        private bool _isConnected;
        private readonly Random _random = new();

        public event Action<string>? RawDataTransmitted;
        public event Action<string>? RawDataReceived;

        public string ConnectionName => "Virtual LTE Module Simulator (Internal)";
        public bool IsConnected => _isConnected;

        // Allows testing failure scenarios (e.g. "SIM_FAIL", "RF_FAIL")
        public string ForceFailureMode { get; set; } = "NONE"; 

        public bool Connect()
        {
            _isConnected = true;
            RawDataReceived?.Invoke("[SIMULATOR] Virtual LTE Module Initialized (Firmware: EC25-V2.1, Ready)");
            return true;
        }

        public void Disconnect()
        {
            _isConnected = false;
            RawDataReceived?.Invoke("[SIMULATOR] Virtual LTE Module Disconnected");
        }

        public async Task<string> SendCommandAsync(string command, int timeoutMs = 2000)
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Simulator is not connected.");
            }

            string cleanCmd = command.Trim();
            RawDataTransmitted?.Invoke($">> {cleanCmd}");

            // Simulate realistic hardware response delay (50ms - 150ms)
            int delay = _random.Next(60, 180);
            await Task.Delay(delay);

            string response;
            switch (cleanCmd.ToUpperInvariant())
            {
                case "AT":
                    response = "AT\r\nOK";
                    break;

                case "AT+CGSN": // Read IMEI
                    response = "8654920401" + _random.Next(10000, 99999) + "\r\nOK";
                    break;

                case "AT+CPIN?": // SIM Card check
                    if (ForceFailureMode == "SIM_FAIL")
                    {
                        response = "+CME ERROR: SIM not inserted\r\nERROR";
                    }
                    else
                    {
                        response = "+CPIN: READY\r\nOK";
                    }
                    break;

                case "AT+CSQ": // Signal Quality Check (RSSI 0-31)
                    if (ForceFailureMode == "RF_FAIL")
                    {
                        response = "+CSQ: 6,99\r\nOK"; // Low signal (< 15 is poor)
                    }
                    else
                    {
                        int rssi = _random.Next(24, 31);
                        response = $"+CSQ: {rssi},99\r\nOK";
                    }
                    break;

                case "AT+CGATT?": // Packet Service Attach
                    response = "+CGATT: 1\r\nOK";
                    break;

                case "AT+COPS?": // Network Operator
                    response = "+COPS: 0,0,\"Viettel 4G LTE\",7\r\nOK";
                    break;

                case "PING":
                case "PING 8.8.8.8":
                    int pingTime = _random.Next(15, 38);
                    response = $"Ping 8.8.8.8: time={pingTime}ms TTL=54 OK";
                    break;

                case "SPEED_TEST":
                    double dl = Math.Round(_random.NextDouble() * 30 + 70, 1);
                    double ul = Math.Round(_random.NextDouble() * 15 + 20, 1);
                    response = $"Throughput: DL={dl} Mbps, UL={ul} Mbps OK";
                    break;

                default:
                    response = "OK";
                    break;
            }

            RawDataReceived?.Invoke($"<< {response.Replace("\r\n", " ")}");
            return response;
        }

        public void Dispose()
        {
            Disconnect();
            GC.SuppressFinalize(this);
        }
    }
}
