using System;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Extensions.DependencyInjection;
using PotopopiCamSync.Models;
using PotopopiCamSync.Services;
using PotopopiCamSync.Repositories;
using PotopopiCamSync.ViewModels;

namespace PotopopiCamSync.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly ISettingsRepository _settings;
        private ObservableCollection<DeviceSignatureModel> _devicesList;
        public ObservableCollection<ImmichAccountModel> ImmichAccountsList { get; } = new();

        public SettingsWindow()
        {
            InitializeComponent();
            _settings = App.ServiceProvider.GetRequiredService<ISettingsRepository>();
            _devicesList = new ObservableCollection<DeviceSignatureModel>();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var config = _settings.Config;
            txtLocalFolder.Text = config.LocalBackupFolder;
            chkDeleteAfter.IsChecked = config.DeleteAfterSync;
            txtKeepDays.Text = config.KeepFilesDays.ToString();

            chkEnableImmich.IsChecked = config.EnableImmichSync;
            txtImmichUrl.Text = config.ImmichUrl;
            txtImmichApiKey.Text = config.ImmichApiKey;

            txtExclusionPatterns.Text = config.ExclusionPatterns;
            txtImmichExclusionPatterns.Text = config.ImmichExclusionPatterns;

            txtDownloadLimit.Text = (config.DownloadSpeedLimitBps / (1024 * 1024)).ToString();
            txtUploadLimit.Text = (config.UploadSpeedLimitBps / (1024 * 1024)).ToString();

            chkStartMinimized.IsChecked = config.StartMinimized;
            chkMinimizeToTray.IsChecked = config.MinimizeToTray;

            // Cloud
            chkEnableGoogleDrive.IsChecked = config.GoogleDriveAccount.IsEnabled;
            txtGoogleFolder.Text = config.GoogleDriveAccount.TargetFolderName;
            UpdateGoogleStatusUI();

            chkEnableOneDrive.IsChecked = config.OneDriveAccount.IsEnabled;
            txtOneDriveFolder.Text = config.OneDriveAccount.TargetFolderName;
            UpdateOneDriveStatusUI();

            txtGoogleClientId.Text = config.GoogleDriveAccount.ClientId;
            txtOneDriveClientId.Text = config.OneDriveAccount.ClientId;

            // AI
            switch (config.AIAnalysisMode)
            {
                case AIAnalysisMode.None: rbAI_None.IsChecked = true; break;
                case AIAnalysisMode.Standard: rbAI_Standard.IsChecked = true; break;
                case AIAnalysisMode.Extreme: rbAI_Extreme.IsChecked = true; break;
                default: rbAI_Standard.IsChecked = true; break;
            }

            // Accounts
            ImmichAccountsList.Clear();
            foreach (var acc in config.ImmichAccounts) ImmichAccountsList.Add(acc);
            lstAccounts.ItemsSource = ImmichAccountsList;

            LoadHardwareInfo();
            RefreshDevicesList();
        }

        private void LoadHardwareInfo()
        {
            try
            {
                var hardware = App.ServiceProvider.GetRequiredService<HardwareDetectionService>();
                var caps = hardware.GetCapabilities();
                txtHardwareInfo.Text = caps.GpuNames.Any() ? $"{string.Join(", ", caps.GpuNames)}\nVRAM: {caps.TotalVramMb} MB" : "No dedicated GPU detected.";
            }
            catch { txtHardwareInfo.Text = "Hardware detection unavailable."; }
        }

        private void RefreshDevicesList()
        {
            _devicesList.Clear();
            foreach (var device in _settings.Config.RegisteredDevices) _devicesList.Add(device);
            lstDevices.ItemsSource = _devicesList;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true) txtLocalFolder.Text = dialog.FolderName;
        }

        private void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtImmichUrl.Text) || string.IsNullOrWhiteSpace(txtImmichApiKey.Text))
            {
                MessageBox.Show("Please enter URL and API Key above to use as a template for the new account.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            
            var newAcc = new ImmichAccountModel 
            { 
                Name = $"Account {ImmichAccountsList.Count + 1}", 
                Url = txtImmichUrl.Text, 
                ApiKey = txtImmichApiKey.Text 
            };
            ImmichAccountsList.Add(newAcc);
        }

        private void RemoveAccount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                var acc = ImmichAccountsList.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
                if (acc != null) ImmichAccountsList.Remove(acc);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var newAiMode = rbAI_Extreme.IsChecked == true ? AIAnalysisMode.Extreme : (rbAI_None.IsChecked == true ? AIAnalysisMode.None : AIAnalysisMode.Standard);
            
            if (newAiMode != AIAnalysisMode.None)
            {
                var depManager = App.ServiceProvider.GetRequiredService<AIDependencyManagerService>();
                if (!depManager.IsInstalled())
                {
                    var result = MessageBox.Show("Fitur AI butuh modul tambahan (Native Libraries, ~45MB).\nDownload sekarang?", "AI Module Required", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        var dlWindow = new AIDownloadWindow { Owner = this };
                        if (dlWindow.ShowDialog() != true)
                        {
                            // Download failed or cancelled
                            rbAI_None.IsChecked = true;
                            return; // Stop saving
                        }
                    }
                    else
                    {
                        // User declined
                        rbAI_None.IsChecked = true;
                        return; // Stop saving
                    }
                }
            }

            var config = _settings.Config;

            config.LocalBackupFolder = txtLocalFolder.Text;
            config.DeleteAfterSync   = chkDeleteAfter.IsChecked ?? false;
            if (int.TryParse(txtKeepDays.Text, out int days)) config.KeepFilesDays = days;

            config.EnableImmichSync = chkEnableImmich.IsChecked ?? false;
            config.ImmichUrl        = txtImmichUrl.Text;
            config.ImmichApiKey     = txtImmichApiKey.Text;

            config.ExclusionPatterns = txtExclusionPatterns.Text;
            config.ImmichExclusionPatterns = txtImmichExclusionPatterns.Text;

            if (long.TryParse(txtDownloadLimit.Text, out long dlMb)) config.DownloadSpeedLimitBps = dlMb * 1024 * 1024;
            if (long.TryParse(txtUploadLimit.Text, out long ulMb)) config.UploadSpeedLimitBps = ulMb * 1024 * 1024;

            config.AIAnalysisMode = newAiMode;

            config.StartMinimized = chkStartMinimized.IsChecked ?? false;
            config.MinimizeToTray = chkMinimizeToTray.IsChecked ?? true;

            config.GoogleDriveAccount.IsEnabled = chkEnableGoogleDrive.IsChecked ?? false;
            config.GoogleDriveAccount.TargetFolderName = txtGoogleFolder.Text;

            config.OneDriveAccount.IsEnabled = chkEnableOneDrive.IsChecked ?? false;
            config.OneDriveAccount.TargetFolderName = txtOneDriveFolder.Text;

            config.GoogleDriveAccount.ClientId = txtGoogleClientId.Text;
            config.OneDriveAccount.ClientId = txtOneDriveClientId.Text;

            // Save accounts
            config.ImmichAccounts = ImmichAccountsList.ToList();

            _settings.SaveConfig();
            App.ServiceProvider.GetRequiredService<MainViewModel>().RefreshCloudStatus();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
        private void RefreshDevices_Click(object sender, RoutedEventArgs e) => RefreshDevicesList();

        private void ToggleAdvanced_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            pnlAdvancedCloud.Visibility = pnlAdvancedCloud.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void LoginGoogle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var googleService = new GoogleDriveSyncService(
                    App.ServiceProvider.GetRequiredService<ILogger<GoogleDriveSyncService>>(),
                    _settings.Config,
                    App.ServiceProvider.GetRequiredService<ITokenStorageService>()
                );
                await googleService.InitializeAsync();
                UpdateGoogleStatusUI();
                if (_settings.Config.GoogleDriveAccount.IsAuthenticated)
                    MessageBox.Show("Successfully authenticated with Google Drive!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Google Login Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async void LoginOneDrive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var oneDriveService = new OneDriveSyncService(
                    App.ServiceProvider.GetRequiredService<ILogger<OneDriveSyncService>>(),
                    _settings.Config,
                    App.ServiceProvider.GetRequiredService<ITokenStorageService>()
                );
                await oneDriveService.InitializeAsync();
                UpdateOneDriveStatusUI();
                if (_settings.Config.OneDriveAccount.IsAuthenticated)
                    MessageBox.Show("Successfully authenticated with OneDrive!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"OneDrive Login Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void UpdateGoogleStatusUI()
        {
            var acc = _settings.Config.GoogleDriveAccount;
            txtGoogleStatus.Text = acc.IsAuthenticated ? "✓ Authenticated" : "✗ Not Authenticated";
            txtGoogleStatus.Foreground = acc.IsAuthenticated ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
            txtGoogleEmail.Text = acc.UserEmail;
        }

        private void UpdateOneDriveStatusUI()
        {
            var acc = _settings.Config.OneDriveAccount;
            txtOneDriveStatus.Text = acc.IsAuthenticated ? "✓ Authenticated" : "✗ Not Authenticated";
            txtOneDriveStatus.Foreground = acc.IsAuthenticated ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
            txtOneDriveEmail.Text = acc.UserEmail;
        }

        private void UnregisterDevice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                var device = _devicesList.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
                if (device != null && MessageBox.Show($"Unregister '{device.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _settings.Config.RegisteredDevices.RemoveAll(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
                    _settings.SaveConfig();
                    RefreshDevicesList();
                }
            }
        }

        private async void TestImmichConnection_Click(object sender, RoutedEventArgs e)
        {
            txtImmichStatus.Text = "Testing...";
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                string url = txtImmichUrl.Text.TrimEnd('/');
                if (!url.EndsWith("/api", StringComparison.OrdinalIgnoreCase)) url += "/api";
                
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, $"{url}/users/me");
                if (!string.IsNullOrWhiteSpace(txtImmichApiKey.Text)) request.Headers.Add("x-api-key", txtImmichApiKey.Text);
                
                var res = await client.SendAsync(request);
                txtImmichStatus.Text = res.IsSuccessStatusCode ? "✓ Success" : $"✗ Failed: {res.StatusCode}";
                txtImmichStatus.Foreground = res.IsSuccessStatusCode ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
            }
            catch (Exception ex) { txtImmichStatus.Text = $"✗ Error: {ex.Message}"; txtImmichStatus.Foreground = System.Windows.Media.Brushes.Red; }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; } catch { }
        }
        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to clear activity logs and AI review history?", "Clear Cache", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _settings.State.PersistentLogs.Clear();
                _settings.State.PersistentFlaggedFiles.Clear();
                _settings.State.AllowedAIRejectedFiles.Clear();
                _settings.SaveState();
                MessageBox.Show("Cache cleared successfully. Please restart the app for changes to take effect in the dashboard.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
