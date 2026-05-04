using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PotopopiCamSync.Services;
using PotopopiCamSync.Repositories;
using PotopopiCamSync.ViewModels;

namespace PotopopiCamSync
{
    public partial class MainWindow : Window
    {
        private readonly ISettingsRepository _settings;

        public MainWindow()
        {
            InitializeComponent();
            _settings = App.ServiceProvider.GetRequiredService<ISettingsRepository>();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Views.SettingsWindow();
            settingsWindow.ShowDialog();
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.Config.MinimizeToTray)
            {
                this.Hide();
            }
            else
            {
                this.WindowState = WindowState.Minimized;
            }
        }

        protected override void OnStateChanged(System.EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized && _settings.Config.MinimizeToTray)
            {
                this.Hide();
            }
            base.OnStateChanged(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // If the user clicks 'X', we hide the window instead of killing the process
            // (Unless it's a shutdown event, but WPF handles that)
            e.Cancel = true;
            this.Hide(); 
        }

        private void ListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListView listView && listView.SelectedItem is Models.FlaggedFileModel file)
            {
                if (System.IO.File.Exists(file.Path))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = file.Path,
                            UseShellExecute = true
                        });
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Could not open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("File not found. It may have been moved or deleted.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}