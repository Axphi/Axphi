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

    // ====== 监听全局的鼠标滚轮事件 (支持以鼠标为轴心缩放) ======
    // ====== 监听全局的鼠标滚轮事件 (带防频闪同步黑科技) ======
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);

        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            if (this.DataContext is MainViewModel vm)
            {
                double mouseX = e.GetPosition(GlobalScroll).X;
                double oldScale = vm.Timeline.ZoomScale;
                double oldOffset = GlobalScroll.Value;

                double newScale = oldScale;
                if (e.Delta > 0) newScale *= 1.1;
                else if (e.Delta < 0) newScale /= 1.1;

                if (newScale < 0.01) newScale = 0.01;
                if (newScale > 100.0) newScale = 100.0;

                // 核心计算公式不变
                double ratio = newScale / oldScale;
                double newOffset = (oldOffset + mouseX) * ratio - mouseX;

                // ================= 【解决频闪的终极黑科技】 =================
                // 1. 我们不再使用 BeginInvoke 延迟了！
                // 2. 为了防止滚动条在同步赋值时把我们截断，我们先“预判”它放大后的总长度，并强行撑开它的 Maximum！
                double expectedNewMaximum = GlobalScroll.Maximum * ratio;
                GlobalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.MaximumProperty, expectedNewMaximum);

                // 3. 现在，Maximum 足够大了，我们立刻在同一帧内，同时更新 比例 和 位置！
                // 这样刻度尺在下一次渲染时，拿到的就是完美匹配的“新比例 + 新坐标”，绝不会产生废片！
                vm.Timeline.ZoomScale = newScale;
                GlobalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, newOffset);

                e.Handled = true;
            }
        }
    }

    // ================= 轨道滚动同步逻辑 =================

    // 用来收集所有右侧轨道的“隐形小滚轮”
    private readonly HashSet<ScrollViewer> _trackScrollViewers = new();

    // 每一行轨道生成时，把自己上报给集合
    private void TrackScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            _trackScrollViewers.Add(sv);
        }
    }

    // 当底部总滚动条被拖拽时，强制所有轨道一起滚！
    private void GlobalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // 拿到当前滚动条的进度值
        double offset = e.NewValue;

        // 让所有收集到的黑轨道同步滚动
        foreach (var sv in _trackScrollViewers)
        {
            sv.ScrollToHorizontalOffset(offset);
        }
    }

}