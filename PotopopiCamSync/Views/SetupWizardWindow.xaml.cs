using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PotopopiCamSync.Services;
using PotopopiCamSync.ViewModels;

namespace PotopopiCamSync.Views
{
    public partial class SetupWizardWindow : Window
    {
        public SetupWizardWindow()
        {
            InitializeComponent();
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            var settings = App.ServiceProvider.GetRequiredService<SettingsService>();
            settings.Config.FirstRunCompleted = true;
            settings.SaveConfig();

            ShowDashboard();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.ShowDialog();

            var settings = App.ServiceProvider.GetRequiredService<SettingsService>();
            settings.Config.FirstRunCompleted = true;
            settings.SaveConfig();

            ShowDashboard();
        }

        private void ShowDashboard()
        {
            var vm = App.ServiceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = vm };
            mainWindow.Show();
            Close();
        }
    }
}
