using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LteTestStation.Hardware
{
    public class SerialCommunicator : IDeviceCommunicator
    {
        private SerialPort? _serialPort;
        private readonly string _portName;
        private readonly int _baudRate;
        private readonly StringBuilder _rxBuffer = new();
        private readonly object _lockObj = new();

        public event Action<string>? RawDataTransmitted;
        public event Action<string>? RawDataReceived;

        public string ConnectionName => $"COM: {_portName} @ {_baudRate} bps";
        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        public SerialCommunicator(string portName = "COM1", int baudRate = 115200)
        {
            _portName = portName;
            _baudRate = baudRate;
        }

        public bool Connect()
        {
            try
            {
                Disconnect();
                _serialPort = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 1000,
                    DtrEnable = true,
                    RtsEnable = true
                };

                _serialPort.DataReceived += OnDataReceived;
                _serialPort.Open();
                return true;
            }
            catch (Exception ex)
            {
                RawDataReceived?.Invoke($"[ERROR] Connect failed: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.DataReceived -= OnDataReceived;
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            catch
            {
                // Ignore disconnect exceptions
            }
        }

        public async Task<string> SendCommandAsync(string command, int timeoutMs = 2000)
        {
            if (!IsConnected || _serialPort == null)
            {
                throw new InvalidOperationException("Serial port is not connected.");
            }

            lock (_lockObj)
            {
                _rxBuffer.Clear();
            }

            string formattedCmd = command.EndsWith("\r") || command.EndsWith("\n") ? command : command + "\r\n";
            RawDataTransmitted?.Invoke($">> {command.Trim()}");
            _serialPort.Write(formattedCmd);

            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                await Task.Delay(50);
                string currentResponse;
                lock (_lockObj)
                {
                    currentResponse = _rxBuffer.ToString();
                }

                // Standard AT response terminator check
                if (currentResponse.Contains("OK") || currentResponse.Contains("ERROR"))
                {
                    return currentResponse.Trim();
                }
            }

            lock (_lockObj)
            {
                return _rxBuffer.ToString().Trim();
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    string incoming = _serialPort.ReadExisting();
                    lock (_lockObj)
                    {
                        _rxBuffer.Append(incoming);
                    }
                    RawDataReceived?.Invoke(incoming);
                }
            }
            catch
            {
                // Port closed or read error
            }
        }

        public void Dispose()
        {
            Disconnect();
            GC.SuppressFinalize(this);
        }
    }
}
