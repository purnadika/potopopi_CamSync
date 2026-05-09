#if ANDROID
using System;

namespace PotopopiCamSync.Services
{
    public class AndroidDeviceMonitorService : IDeviceMonitorService
    {
        public event Action<IDeviceProvider>? OnDeviceConnected;

        public void Start()
        {
            // TODO: Implement Android USB Host / BroadcastReceiver for device attachment
        }

        public void Stop()
        {
            // TODO: Unregister receivers
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
#endif
