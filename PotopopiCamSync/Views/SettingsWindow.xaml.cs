using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PotopopiCamSync.Services;
using PotopopiCamSync.ViewModels;

namespace PotopopiCamSync.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settings;

        public SettingsWindow()
        {
            InitializeComponent();
            _settings = App.ServiceProvider.GetRequiredService<SettingsService>();
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

            lstDevices.ItemsSource = config.RegisteredDevices;
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
    }
}
