using Axphi.Data;
using Axphi.Services;
using Axphi.Utilities;
using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Utils;
using NAudio.Wave;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Globalization;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging; // 必须加上这个


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


        WeakReferenceMessenger.Default.Register<AudioLoadedMessage>(this, (r, message) =>
        {
            // 收到 ViewModel 发来的消息后，让 UI 控件去加载音频
            MainChartDisplay.LoadAudio(message.FilePath);
        });

        
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
        hwndSource.CompositionTarget.BackgroundColor = Color.FromRgb(31, 31, 31);
        base.OnSourceInitialized(e);
    }

    // ====== 新增：监听全局的鼠标滚轮事件 ======
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);

        // 检查是不是按住了 Alt 键
        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            // 获取大管家 ViewModel
            if (this.DataContext is MainViewModel vm)
            {
                // 滚轮向上 (放大)，每次放大 10%
                if (e.Delta > 0)
                {
                    vm.Timeline.ZoomScale *= 1.1;
                }
                // 滚轮向下 (缩小)，每次缩小 10%
                else if (e.Delta < 0)
                {
                    vm.Timeline.ZoomScale /= 1.1;
                }

                // 标记事件已处理，防止触发页面上下滚动
                e.Handled = true;
            }
        }
    }

}