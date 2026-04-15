using Axphi.ViewModels;
using System.Windows;

namespace Axphi;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _mainViewModel;

    public MainWindow(
        MainViewModel mainViewModel)
    {
        InitializeComponent();

        _mainViewModel = mainViewModel;
        DataContext = mainViewModel;
    }
}