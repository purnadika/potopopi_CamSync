using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PotopopiCamSync.Models;
using PotopopiCamSync.Services;
using PotopopiCamSync.ViewModels;

namespace PotopopiCamSync.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settings;
        private ObservableCollection<DeviceSignature> _devicesList;

        public SettingsWindow()
        {
            InitializeComponent();
            _settings = App.ServiceProvider.GetRequiredService<SettingsService>();
            _devicesList = new ObservableCollection<DeviceSignature>();
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

            RefreshDevicesList();
        }

        private void RefreshDevicesList()
        {
            _devicesList.Clear();
            foreach (var device in _settings.Config.RegisteredDevices)
            {
                _devicesList.Add(device);
            }

            lstDevices.ItemsSource = _devicesList;

            // Show "no devices" message if empty
            txtNoDevices.Visibility = _devicesList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Local Backup Folder",
                InitialDirectory = string.IsNullOrEmpty(txtLocalFolder.Text)
                    ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures)
                    : txtLocalFolder.Text
            };

            if (dialog.ShowDialog() == true)
                txtLocalFolder.Text = dialog.FolderName;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validate: Immich requires a local folder to be set
            bool immichEnabled = chkEnableImmich.IsChecked ?? false;
            if (immichEnabled && string.IsNullOrWhiteSpace(txtLocalFolder.Text))
            {
                MessageBox.Show(
                    "A Local Backup Folder must be configured before enabling Immich sync.\n\nFiles are downloaded locally first, then streamed to Immich.",
                    "Local Folder Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var config = _settings.Config;
            config.LocalBackupFolder = txtLocalFolder.Text;
            config.DeleteAfterSync   = chkDeleteAfter.IsChecked ?? false;
            if (int.TryParse(txtKeepDays.Text, out int days))
                config.KeepFilesDays = days;

            config.EnableImmichSync = immichEnabled;
            config.ImmichUrl        = txtImmichUrl.Text;
            config.ImmichApiKey     = txtImmichApiKey.Text;

            _settings.SaveConfig();

            // Refresh the dashboard button visibility
            var vm = App.ServiceProvider.GetRequiredService<MainViewModel>();
            vm.RefreshImmichStatus();

            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void RefreshDevices_Click(object sender, RoutedEventArgs e)
        {
            RefreshDevicesList();
            MessageBox.Show(
                "Device list refreshed.",
                "Refresh Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void UnregisterDevice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string deviceId)
                return;

            // Find device to confirm
            var device = _devicesList.FirstOrDefault(d => d.Id == deviceId);
            if (device is null)
                return;

            var result = MessageBox.Show(
                $"Are you sure you want to unregister device '{device.Name}' ({device.Id})?\n\n" +
                "This will remove it from the registered devices list but will NOT delete any synced files.",
                "Confirm Unregister",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result == MessageBoxResult.Yes)
            {
                // Remove from config
                _settings.Config.RegisteredDevices.RemoveAll(d => d.Id == deviceId);
                _settings.SaveConfig();

                // Refresh UI
                RefreshDevicesList();

                MessageBox.Show(
                    $"Device '{device.Name}' has been unregistered.",
                    "Device Unregistered",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private async void TestImmichConnection_Click(object sender, RoutedEventArgs e)
        {
            txtImmichStatus.Visibility = Visibility.Visible;
            txtImmichStatus.Text = "Testing connection...";
            txtImmichStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);

            string url = txtImmichUrl.Text.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(url))
            {
                txtImmichStatus.Text = "Please enter a valid URL.";
                txtImmichStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                return;
            }

            if (!url.EndsWith("/api", System.StringComparison.OrdinalIgnoreCase))
                url += "/api";
            
            string apiKey = txtImmichApiKey.Text;

            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = System.TimeSpan.FromSeconds(10);
                
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, $"{url}/users/me");
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers.Add("x-api-key", apiKey);
                }
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    txtImmichStatus.Text = "Connection successful! API Key is valid.";
                    txtImmichStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    txtImmichStatus.Text = "Connected to server, but API Key is invalid (Unauthorized).";
                    txtImmichStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    txtImmichStatus.Text = "Server reached, but Immich API not found. Check the URL.";
                    txtImmichStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
                else
                {
                    txtImmichStatus.Text = $"Connection failed: Server returned {response.StatusCode}";
                    txtImmichStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
            }
            catch (System.Exception ex)
            {
                txtImmichStatus.Text = $"Connection failed: {ex.Message}";
                txtImmichStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }
    }
}
