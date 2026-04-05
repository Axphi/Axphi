using System;
using System.Windows;

namespace Axphi.Views.Dialogs;

public partial class OfficialChartExportProgressDialog : Window
{
    public OfficialChartExportProgressDialog(bool setupMode = false)
    {
        InitializeComponent();
        if (setupMode)
        {
            SetSetupMode();
        }
        else
        {
            SetProgressMode();
        }
    }

    public bool CalculateFloorPosition => CalculateFloorPositionCheckBox.IsChecked == true;

    public void UpdateProgress(double fraction, string message)
    {
        SetProgressMode();
        double clamped = Math.Clamp(fraction, 0.0, 1.0);
        ProgressBar.Value = clamped * 100.0;
        PercentTextBlock.Text = $"{Math.Round(clamped * 100.0):0}%";
        StatusTextBlock.Text = string.IsNullOrWhiteSpace(message) ? "准备导出官谱..." : message;
    }

    private void RenderButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void SetSetupMode()
    {
        TitleTextBlock.Text = "导出官谱";
        SetupPanel.Visibility = Visibility.Visible;
        RenderButton.Visibility = Visibility.Visible;
        ProgressPanel.Visibility = Visibility.Collapsed;
        StatusTextBlock.Visibility = Visibility.Collapsed;
        Height = 170;
    }

    private void SetProgressMode()
    {
        TitleTextBlock.Text = "导出官谱中";
        SetupPanel.Visibility = Visibility.Collapsed;
        RenderButton.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        StatusTextBlock.Visibility = Visibility.Visible;
        Height = 150;
    }
}