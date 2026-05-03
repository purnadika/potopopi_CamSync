using System.Windows;

namespace PotopopiCamSync.Views
{
    public partial class AIReviewWindow : Window
    {
        public AIReviewWindow()
        {
            InitializeComponent();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            vm?.OpenLocalFolderCommand.Execute(null);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
