using CommunityToolkit.Mvvm.ComponentModel;

namespace Axphi.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private double _x1;
    [ObservableProperty]
    private double _y1;
    [ObservableProperty]
    private double _x2;
    [ObservableProperty]
    private double _y2;


}
