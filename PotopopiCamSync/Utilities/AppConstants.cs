using System;

namespace PotopopiCamSync.Utilities
{
    public static class AppConstants
    {
        public static class General
        {
            public const string ApplicationName = "Potopopi CamSync";
            public const string InternalName = "PotopopiCamSync";
            public const string MutexName = InternalName + "_SingleInstanceMutex";
            public const string ShowWindowEventName = InternalName + "_ShowWindowEvent";
            public const string TrayTitle = "Potopopi CamSync";
            public const string AlreadyRunningTitle = "Already Running";
            public const string SyncCompleteTitle = "Sync Complete";
            public const string DeviceConnectedTitle = "Device Connected";
            public const string RunningBackgroundTitle = "Running in Background";
            public static string AppVersion => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        }


        public static class MenuItems
        {
            public const string OpenDashboard = "Open Dashboard";
            public const string Exit = "Exit";
        }

        public static class Identifiers
        {
            public const string LocalBackupSourceId = "__local_backup__";
            public const string LocalBackupAccountId = "local-backup";
        }
    }
}
