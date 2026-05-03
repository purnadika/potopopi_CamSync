using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using PotopopiCamSync.Services;
using PotopopiCamSync.Repositories;
using PotopopiCamSync.ViewModels;
using PotopopiCamSync.Views;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Drawing;

namespace PotopopiCamSync
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private readonly IHost _host;
        private System.Threading.Mutex? _mutex;
        private System.Threading.EventWaitHandle? _instanceEvent;

        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(new FileLoggerProvider());
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
                    services.AddSingleton<HardwareDetectionService>();
                    services.AddSingleton<IMediaAnalyzer, AIEngine>();
                    services.AddSingleton<DeviceMonitorService>();
                    services.AddSingleton<SyncOrchestrator>();
                    services.AddSingleton<MainViewModel>();
                    services.AddHttpClient<UpdateChecker>();
                })
                .Build();
            
            ServiceProvider = _host.Services;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Hardware Detection and AI Mode setup
            try
            {
                var hardware = ServiceProvider.GetRequiredService<HardwareDetectionService>();
                var startupSettings = ServiceProvider.GetRequiredService<ISettingsRepository>();
                
                var caps = hardware.GetCapabilities();
                if (startupSettings.Config.AIAnalysisMode == Models.AIAnalysisMode.None)
                {
                    startupSettings.Config.AIAnalysisMode = caps.SuggestedMode;
                    startupSettings.SaveConfig();
                }
            }
            catch { }

            const string appName = "PotopopiCamSync_SingleInstanceMutex";
            const string eventName = "PotopopiCamSync_ShowWindowEvent";
            
            _mutex = new System.Threading.Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                try
                {
                    var existingEvent = System.Threading.EventWaitHandle.OpenExisting(eventName);
                    existingEvent.Set();
                }
                catch { }

                MessageBox.Show("Potopopi CamSync is already running!", "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            _instanceEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, eventName);
            System.Threading.ThreadPool.RegisterWaitForSingleObject(_instanceEvent, 
                (state, timeout) => Dispatcher.Invoke(() => ShowMainWindow()), null, -1, false);

            base.OnStartup(e);
            await _host.StartAsync();

            var settings = ServiceProvider.GetRequiredService<ISettingsRepository>();
            var deviceMonitor = ServiceProvider.GetRequiredService<DeviceMonitorService>();
            var updateChecker = ServiceProvider.GetRequiredService<UpdateChecker>();

            // Initialize System Tray Icon
            _notifyIcon = new TaskbarIcon
            {
                ToolTipText = "Potopopi CamSync",
                Icon = Icon.ExtractAssociatedIcon(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "") ?? SystemIcons.Information
            };
            
            _notifyIcon.TrayLeftMouseDown += (s, args) => ShowMainWindow();

            var contextMenu = new System.Windows.Controls.ContextMenu();
            var openItem = new System.Windows.Controls.MenuItem { Header = "Open Dashboard", FontWeight = FontWeights.Bold };
            openItem.Click += (s, args) => ShowMainWindow();
            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += async (s, args) => 
            {
                _notifyIcon.Dispose();
                await _host.StopAsync();
                _host.Dispose();
                Current.Shutdown();
            };
            contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(exitItem);
            _notifyIcon.ContextMenu = contextMenu;

            // Handle background notifications from services
            deviceMonitor.OnDeviceConnected += (device) => 
            {
                ShowNotification("Device Connected", $"Detected {device.DeviceName}. Starting sync...");
            };

            // Start monitor
            deviceMonitor.Start();

            if (!settings.Config.FirstRunCompleted)
            {
                new SetupWizardWindow().Show();
            }
            else if (!settings.Config.StartMinimized)
            {
                ShowMainWindow(); 
            }
            else
            {
                ShowNotification("Running in Background", "Potopopi CamSync is active in the system tray.");
            }
        }

        public void ShowNotification(string title, string message, BalloonIcon icon = BalloonIcon.Info)
        {
            Dispatcher.Invoke(() => 
            {
                _notifyIcon?.ShowBalloonTip(title, message, icon);
            });
        }

        public static void ShowTrayNotification(string title, string message)
        {
            ((App)Application.Current).ShowNotification(title, message);
        }

        public void ShowMainWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mw)
                {
                    mw.Show();
                    mw.WindowState = WindowState.Normal;
                    mw.Activate();
                    return;
                }
            }

            var newMw = new MainWindow { DataContext = ServiceProvider.GetRequiredService<MainViewModel>() };
            newMw.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            if (_mutex != null) { _mutex.ReleaseMutex(); _mutex.Dispose(); }
            if (_instanceEvent != null) _instanceEvent.Dispose();
            base.OnExit(e);
        }
    }
}
