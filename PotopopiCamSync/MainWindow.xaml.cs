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
    }
}