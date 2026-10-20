using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LteTestStation.Hardware
{
    public class SocketCommunicator : IDeviceCommunicator
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private readonly string _ipAddress;
        private readonly int _port;

        public event Action<string>? RawDataTransmitted;
        public event Action<string>? RawDataReceived;

        public string ConnectionName => $"TCP/IP: {_ipAddress}:{_port}";
        public bool IsConnected => _tcpClient != null && _tcpClient.Connected;

        public SocketCommunicator(string ipAddress = "192.168.1.1", int port = 8080)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        public bool Connect()
        {
            try
            {
                Disconnect();
                _tcpClient = new TcpClient();
                var connectTask = _tcpClient.ConnectAsync(_ipAddress, _port);
                bool completed = connectTask.Wait(2000);
                if (!completed || !_tcpClient.Connected)
                {
                    Disconnect();
                    return false;
                }
                _stream = _tcpClient.GetStream();
                return true;
            }
            catch (Exception ex)
            {
                RawDataReceived?.Invoke($"[ERROR] Socket Connect failed: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                _stream?.Dispose();
                _stream = null;
                _tcpClient?.Dispose();
                _tcpClient = null;
            }
            catch
            {
                // Ignore
            }
        }

        public async Task<string> SendCommandAsync(string command, int timeoutMs = 2000)
        {
            if (!IsConnected || _stream == null)
            {
                throw new InvalidOperationException("Socket is not connected.");
            }

            string sendCmd = command.EndsWith("\n") ? command : command + "\r\n";
            byte[] data = Encoding.ASCII.GetBytes(sendCmd);
            RawDataTransmitted?.Invoke($">> {command.Trim()}");
            await _stream.WriteAsync(data, 0, data.Length);

            byte[] buffer = new byte[2048];
            using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
            int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
            string response = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
            RawDataReceived?.Invoke(response);
            return response;
        }

        public void Dispose()
        {
            Disconnect();
            GC.SuppressFinalize(this);
        }
    }
}
