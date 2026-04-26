using System.Windows;
using PotopopiCamSync.Services;

namespace PotopopiCamSync.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var config = App.Settings.Config;
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
                InitialDirectory = string.IsNullOrEmpty(txtLocalFolder.Text) ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures) : txtLocalFolder.Text
            };
            
            if (dialog.ShowDialog() == true)
            {
                txtLocalFolder.Text = dialog.FolderName;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var config = App.Settings.Config;
            config.LocalBackupFolder = txtLocalFolder.Text;
            config.DeleteAfterSync = chkDeleteAfter.IsChecked ?? false;
            if (int.TryParse(txtKeepDays.Text, out int days))
            {
                config.KeepFilesDays = days;
            }
            
            config.EnableImmichSync = chkEnableImmich.IsChecked ?? false;
            config.ImmichUrl = txtImmichUrl.Text;
            config.ImmichApiKey = txtImmichApiKey.Text;

            App.Settings.SaveConfig();
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
