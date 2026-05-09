using System;

namespace PotopopiCamSync.Services
{
    public interface IDeviceMonitorService : IDisposable
    {
        event Action<IDeviceProvider>? OnDeviceConnected;
        void Start();
        void Stop();
    }
}
