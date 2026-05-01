using System.Windows;

namespace PotopopiCamSync.Views;

public partial class LoadingWindow : Window
{
    public LoadingWindow()
    {
        InitializeComponent();
    }

    public void UpdateStatus(string status, string? detail = null)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = status;
            if (detail != null)
                DetailText.Text = detail;
        });
    }

    public new void Close()
    {
        Dispatcher.Invoke(() => base.Close());
    }
}
