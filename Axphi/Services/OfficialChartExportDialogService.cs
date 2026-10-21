using Axphi.Views.Dialogs;
using System.Windows;

namespace Axphi.Services;

public sealed class OfficialChartExportDialogService : IOfficialChartExportDialogService
{
    public OfficialChartExportSettings? ShowSetupDialog()
    {
        var dialog = new OfficialChartExportProgressDialog(setupMode: true)
        {
            Owner = GetOwner()
        };

        bool? confirmed = dialog.ShowDialog();
        return confirmed == true
            ? new OfficialChartExportSettings(dialog.CalculateFloorPosition)
            : null;
    }

    public IOfficialChartExportProgressSession CreateProgressSession()
    {
        return new OfficialChartExportProgressSession(GetOwner());
    }

    private static Window? GetOwner()
    {
        return Application.Current?.MainWindow;
    }

    private sealed class OfficialChartExportProgressSession : IOfficialChartExportProgressSession
    {
        private readonly OfficialChartExportProgressDialog _dialog;
        private bool _disposed;

        public OfficialChartExportProgressSession(Window? owner)
        {
            _dialog = new OfficialChartExportProgressDialog();
            if (owner is not null)
            {
                _dialog.Owner = owner;
            }

            _dialog.UpdateProgress(0.0, "准备导出官谱...");
        }

        public void Show()
        {
            _dialog.Show();
        }

        public void Update(double fraction, string message)
        {
            _dialog.UpdateProgress(fraction, message);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_dialog.IsVisible)
            {
                _dialog.Close();
            }
        }
    }
}
