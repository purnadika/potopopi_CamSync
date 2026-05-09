#if IOS || MACCATALYST
using System;

namespace PotopopiCamSync.Services
{
    public class IOSDeviceMonitorService : IDeviceMonitorService
    {
        public event Action<IDeviceProvider>? OnDeviceConnected;

        public void Start()
        {
            // TODO: Implement ExternalAccessory framework or similar for iOS
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
#endif
