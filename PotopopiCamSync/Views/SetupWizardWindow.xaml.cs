using System.Windows;

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
            App.Settings.Config.FirstRunCompleted = true;
            App.Settings.SaveConfig();
            
            var mainWindow = new MainWindow();
            mainWindow.DataContext = App.MainViewModel;
            mainWindow.Show();
            this.Close();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            // For now, just open settings and pretend it's the wizard flow
            var settings = new SettingsWindow();
            settings.ShowDialog();
            
            App.Settings.Config.FirstRunCompleted = true;
            App.Settings.SaveConfig();
            
            var mainWindow = new MainWindow();
            mainWindow.DataContext = App.MainViewModel;
            mainWindow.Show();
            this.Close();
        }
    }
}
