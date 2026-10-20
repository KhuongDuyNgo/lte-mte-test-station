using System;
using System.Threading.Tasks;

namespace LteTestStation.Hardware
{
    public interface IDeviceCommunicator : IDisposable
    {
        string ConnectionName { get; }
        bool IsConnected { get; }

        bool Connect();
        void Disconnect();
        Task<string> SendCommandAsync(string command, int timeoutMs = 2000);

        event Action<string>? RawDataTransmitted;
        event Action<string>? RawDataReceived;
    }
}
