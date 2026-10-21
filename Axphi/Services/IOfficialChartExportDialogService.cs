using System;

namespace Axphi.Services;

public readonly record struct OfficialChartExportSettings(bool CalculateFloorPosition);

public interface IOfficialChartExportDialogService
{
    OfficialChartExportSettings? ShowSetupDialog();

    IOfficialChartExportProgressSession CreateProgressSession();
}

public interface IOfficialChartExportProgressSession : IDisposable
{
    void Show();

    void Update(double fraction, string message);
}
