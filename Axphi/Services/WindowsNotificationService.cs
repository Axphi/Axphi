using System.Windows;

namespace Axphi.Services;

public sealed class WindowsNotificationService : INotificationService
{
    public void ShowError(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
