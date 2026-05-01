using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using PotopopiCamSync.Services;
using PotopopiCamSync.ViewModels;
using PotopopiCamSync.Views;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PotopopiCamSync
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private readonly IHost _host;
        private System.Threading.Mutex? _mutex;

        public static IServiceProvider ServiceProvider { get; private set; }

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
                    services.AddSingleton<SettingsService>();
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
            const string appName = "PotopopiCamSync_SingleInstanceMutex";
            _mutex = new System.Threading.Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                MessageBox.Show("Potopopi CamSync is already running! Please check your system tray (bottom right corner).", "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            await _host.StartAsync();

            var settings = ServiceProvider.GetRequiredService<SettingsService>();
            var deviceMonitor = ServiceProvider.GetRequiredService<DeviceMonitorService>();
            var updateChecker = ServiceProvider.GetRequiredService<UpdateChecker>();

            // Background update check (non-blocking)
            _ = updateChecker.CheckForUpdateAsync()
                .ContinueWith(task =>
                {
                    if (task.Result != null)
                    {
                        MessageBox.Show(
                            $"Update available: {task.Result.LatestVersion}\n\nRelease notes:\n{task.Result.ReleaseNotes}",
                            "Potopopi CamSync - Update Available",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());

            // Initialize System Tray Icon
            _notifyIcon = new TaskbarIcon
            {
                ToolTipText = "Potopopi CamSync",
                Icon = System.Drawing.SystemIcons.Information // Using default icon for now
            };
            
            _notifyIcon.TrayLeftMouseDown += (s, args) =>
            {
                ShowMainWindow();
            };

            // Setup Context Menu for Tray Icon
            var contextMenu = new System.Windows.Controls.ContextMenu();
            var openItem = new System.Windows.Controls.MenuItem { Header = "Open Dashboard" };
            openItem.Click += (s, args) => ShowMainWindow();
            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += async (s, args) => 
            {
                deviceMonitor.Stop();
                deviceMonitor.Dispose();
                _notifyIcon.Dispose();
                await _host.StopAsync();
                _host.Dispose();
                Current.Shutdown();
            };
            contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(exitItem);
            _notifyIcon.ContextMenu = contextMenu;

            // Show UI or hide
            if (!settings.Config.FirstRunCompleted)
            {
                var wizard = new SetupWizardWindow();
                wizard.Show();
            }
            else
            {
                // Run in background, but show main window on manual launch
                ShowMainWindow(); 
            }
        }

        private void ShowMainWindow()
        {
            // Find existing
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
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
