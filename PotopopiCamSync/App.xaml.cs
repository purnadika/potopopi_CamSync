using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using PotopopiCamSync.Services;
using PotopopiCamSync.ViewModels;
using PotopopiCamSync.Views;

namespace PotopopiCamSync
{
    public partial class App : Application
    {
        private TaskbarIcon _notifyIcon;
        public static SettingsService Settings { get; private set; }
        public static MainViewModel MainViewModel { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize services
            Settings = new SettingsService();
            var orchestrator = new SyncOrchestrator(Settings);
            var deviceMonitor = new DeviceMonitorService();
            
            MainViewModel = new MainViewModel(orchestrator, deviceMonitor, Settings);

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
            exitItem.Click += (s, args) => 
            {
                deviceMonitor.Stop();
                deviceMonitor.Dispose();
                _notifyIcon.Dispose();
                Current.Shutdown();
            };
            contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(exitItem);
            _notifyIcon.ContextMenu = contextMenu;

            // Show UI or hide
            if (!Settings.Config.FirstRunCompleted)
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

            var newMw = new MainWindow { DataContext = MainViewModel };
            newMw.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
