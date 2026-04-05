using System;
using System.Windows;

namespace Axphi.Views.Dialogs;

public partial class OfficialChartExportProgressDialog : Window
{
    public OfficialChartExportProgressDialog()
    {
        InitializeComponent();
    }

    public void UpdateProgress(double fraction, string message)
    {
        double clamped = Math.Clamp(fraction, 0.0, 1.0);
        ProgressBar.Value = clamped * 100.0;
        PercentTextBlock.Text = $"{Math.Round(clamped * 100.0):0}%";
        StatusTextBlock.Text = string.IsNullOrWhiteSpace(message) ? "准备导出官谱..." : message;
    }
}