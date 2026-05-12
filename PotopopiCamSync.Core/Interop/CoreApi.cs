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
        [System.Diagnostics.CodeAnalysis.DynamicDependency("Init")]
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
        [System.Diagnostics.CodeAnalysis.DynamicDependency("IsInitialized")]
        [UnmanagedCallersOnly(EntryPoint = "isInitialized")]
        public static int IsInitialized()
        {
            return _orchestrator != null ? 1 : 0;
        }

        /// <summary>
        /// Starts a sync process using a pre-provided list of files (JSON).
        /// Used for Android Scoped Storage compatibility.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.DynamicDependency("StartSyncWithFiles")]
        [UnmanagedCallersOnly(EntryPoint = "startSyncWithFiles")]
        public static unsafe void StartSyncWithFiles(byte* deviceId, byte* deviceName, byte* filesJson, IntPtr progressCallbackPtr)
        {
            if (_orchestrator == null || progressCallbackPtr == IntPtr.Zero) return;

            var progressCallback = (delegate* unmanaged<byte*, void>)progressCallbackPtr;

            string id = Marshal.PtrToStringUTF8((IntPtr)deviceId) ?? "";
            string name = Marshal.PtrToStringUTF8((IntPtr)deviceName) ?? "";
            string json = Marshal.PtrToStringUTF8((IntPtr)filesJson) ?? "[]";

            try
            {
                var files = System.Text.Json.JsonSerializer.Deserialize(json, SourceGenerationContext.Default.ListSyncFileModel) ?? new List<SyncFileModel>();
                var manualDevice = new ManualDeviceProvider(id, name, files);

                _orchestrator.OnSyncProgress += (msg) => {
                    byte[] utf8Bytes = Encoding.UTF8.GetBytes(msg + "\0");
                    fixed (byte* pMsg = utf8Bytes)
                    {
                        progressCallback(pMsg);
                    }
                };

                Task.Run(() => _orchestrator.StartSyncAsync(manualDevice));
            }
            catch (Exception ex)
            {
                byte[] utf8Bytes = Encoding.UTF8.GetBytes($"Error parsing file list: {ex.Message}\0");
                fixed (byte* pMsg = utf8Bytes) { progressCallback(pMsg); }
            }
        }
    }
}
