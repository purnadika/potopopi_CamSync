using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PotopopiCamSync.Services;

namespace PotopopiCamSync.Views
{
    public partial class AIDownloadWindow : Window
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly AIDependencyManagerService _dependencyManager;

        public AIDownloadWindow()
        {
            InitializeComponent();
            _dependencyManager = App.ServiceProvider.GetRequiredService<AIDependencyManagerService>();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var progress = new Progress<double>(p =>
                {
                    pbDownload.Value = p;
                    txtStatus.Text = $"Downloading... {p:0}%";
                    if (p >= 95) txtStatus.Text = "Extracting...";
                });

                await _dependencyManager.DownloadAndInstallAsync(progress, _cts.Token);
                
                DialogResult = true;
                Close();
            }
            catch (OperationCanceledException)
            {
                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download AI module: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            txtStatus.Text = "Cancelling...";
        }
    }
}
