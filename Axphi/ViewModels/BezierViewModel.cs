using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Axphi.ViewModels;

public partial class BezierViewModel : ObservableObject
{
    [ObservableProperty]
    private double _x1;
    [ObservableProperty]
    private double _y1;
    [ObservableProperty]
    private double _x2;
    [ObservableProperty]
    private double _y2;

    public BezierViewModel()
    {
        // ≥ı º÷µ
        X1 = 0.75;
        Y1 = 0.25;
        X2 = 0.25;
        Y2 = 0.75;
    }


    partial void OnX1Changed(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return;
        }

        var clamped = Math.Clamp(value, 0.0, 1.0);
        if (!clamped.Equals(value))
        {
            X1 = clamped;
        }
    }

    partial void OnX2Changed(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return;
        }

        var clamped = Math.Clamp(value, 0.0, 1.0);
        if (!clamped.Equals(value))
        {
            X2 = clamped;
        }
    }
}
