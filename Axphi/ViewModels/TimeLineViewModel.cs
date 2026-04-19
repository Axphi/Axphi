using Axphi.Data;
using Axphi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace Axphi.ViewModels
{
    public partial class TimeLineViewModel : ObservableObject, IDisposable
    {

        // === 🌟 新增：时间轴相机的当前位置 ===
        private Point _viewportLocation;
        public Point ViewportLocation
        {
            get => _viewportLocation;
            set
            {
                if (SetProperty(ref _viewportLocation, value))
                {
                    // 把自己当前的 Y 同步给全局服务
                    _layoutService.UpdateViewportY(value.Y, this);
                }
            }
        }


        // === 1. 视图参数 ===
        [ObservableProperty]
        private double _pixelPerTick = 0.5; // 缩放核心：1个Tick在1.0缩放率下等于0.5像素

        [ObservableProperty]
        private double _zoom = 1.0;

        // === 2. 依赖注入 ===
        private readonly ProjectManager _projectManager;
        private readonly TrackLayoutService _layoutService;

        // === 3. 提供给 UI 的轨道块列表 ===
        public ObservableCollection<TimeLineJudgmentLineViewModel> LineBlocks { get; } = new();

        public TimeLineViewModel(ProjectManager projectManager, TrackLayoutService layoutService)
        {
            _projectManager = projectManager;
            _layoutService = layoutService;


            // 🌟 监听服务的相机同步广播
            _layoutService.ViewportYChanged += (sender, newY) =>
            {
                // 核心防抖：如果广播是我自己发出的，或者 Y 根本没变，就不处理
                if (sender != this && Math.Abs(_viewportLocation.Y - newY) > 0.01)
                {
                    // 右侧的 X 必须保留自己原本的进度，只替换 Y
                    ViewportLocation = new Point(_viewportLocation.X, newY);
                }
            };



            // 监听工程变更，确保谱面切换时能够同步刷新
            _projectManager.PropertyChanged += OnProjectManagerPropertyChanged;

            // 监听布局服务，防止在缩放或调整 X 轴时，错过 Y 轴的变动（双保险）
            _layoutService.LayoutUpdated += OnLayoutUpdated;

            RefreshLineBlocks();
        }

        private void OnProjectManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_projectManager.EditingProject) || e.PropertyName == "Chart")
            {
                RefreshLineBlocks();
            }
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            // 虽然每个子 Block 自己会监听 Y，但有时需要父级配合触发
            // 这里暂不执行，交给 TimeLineJudgmentLineViewModel 自己处理 Y 的变化。
        }

        private void RefreshLineBlocks()
        {
            // 清理旧资源，防止事件泄漏
            foreach (var block in LineBlocks)
            {
                block.Dispose();
            }
            LineBlocks.Clear();

            var chart = _projectManager.EditingProject?.Chart;
            if (chart?.JudgementLines == null) return;

            // 基于底层的 JudgementLine 创建对应的 Timeline Block
            foreach (var line in chart.JudgementLines)
            {
                var blockVM = new TimeLineJudgmentLineViewModel(line, this, _layoutService);
                LineBlocks.Add(blockVM);
            }

            // 注意：这里**不需要**调用 _layoutService.InitializeLines()
            // 因为这是左侧 Header 的专利。Timeline 只负责读。
        }

        // === 当用户使用快捷键/鼠标滚轮改变缩放时 ===
        partial void OnZoomChanged(double value) => RefreshAllBlocksVisuals();
        partial void OnPixelPerTickChanged(double value) => RefreshAllBlocksVisuals();

        private void RefreshAllBlocksVisuals()
        {
            foreach (var block in LineBlocks)
            {
                block.UpdateVisuals();
            }
        }

        public void Dispose()
        {
            _projectManager.PropertyChanged -= OnProjectManagerPropertyChanged;
            _layoutService.LayoutUpdated -= OnLayoutUpdated;
        }
    }
}