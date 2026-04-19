using Axphi.ViewModels;
using System.Windows;
using System.Windows.Input;

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


    // 拦截 Alt 键的系统级行为
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 当按下 Alt 键时，它会被识别为 SystemKey
        if (e.Key == Key.System && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt))
        {
            // 把事件标记为已处理，这样焦点就不会被标题栏抢走了
            e.Handled = true;
        }
    }
}