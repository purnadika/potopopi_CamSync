using System;
using System.Runtime.InteropServices;
using System.Text;
using PotopopiCamSync.Services;
using PotopopiCamSync.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace PotopopiCamSync.Interop
{
    public static class CoreApi
    {
        private static SyncOrchestratorService? _orchestrator;
        private static JsonSettingsRepository? _settings;

        /// <summary>
        /// Initializes the core engine.
        /// </summary>
        /// <param name="settingsPath">Path to the settings directory (UTF-8).</param>
        [UnmanagedCallersOnly(EntryPoint = "init")]
        public static unsafe void Init(byte* settingsPath)
        {
            try
            {
                string path = Marshal.PtrToStringUTF8((IntPtr)settingsPath) ?? "";
                _settings = new JsonSettingsRepository(path);
                
                // For now, using NullLogger. Native app can implement a logger later.
                _orchestrator = new SyncOrchestratorService(
                    _settings, 
                    null!, // MediaAnalyzer (to be injected)
                    NullLogger<SyncOrchestratorService>.Instance,
                    NullLoggerFactory.Instance);
            }
            catch (Exception)
            {
                // Handle error or return code
            }
        }

        /// <summary>
        /// Checks if the engine is initialized.
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = "isInitialized")]
        public static int IsInitialized()
        {
            return _orchestrator != null ? 1 : 0;
        }

        /// <summary>
        /// Starts a sync process for the given device.
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = "startSync")]
        public static unsafe void StartSync(byte* deviceId, IntPtr progressCallbackPtr)
        {
            if (_orchestrator == null || progressCallbackPtr == IntPtr.Zero) return;

            // Cast the IntPtr back to an unmanaged function pointer
            var progressCallback = (delegate* unmanaged<byte*, void>)progressCallbackPtr;

            string id = Marshal.PtrToStringUTF8((IntPtr)deviceId) ?? "";
            
            _orchestrator.OnSyncProgress += (msg) => {
                byte[] utf8Bytes = Encoding.UTF8.GetBytes(msg + "\0"); // Null-terminated for C
                fixed (byte* pMsg = utf8Bytes)
                {
                    progressCallback(pMsg);
                }
            };
        }
    }
}
