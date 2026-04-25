using Axphi.Data;
using Axphi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Axphi.Data.KeyFrames; // 确保引用了你的 IKeyFrame

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
                // 防止 timeLineRuler 抖动
                // 在 VM 层直接钳制，绝对不允许非法值污染全局状态！
                Point clampedValue = new Point(Math.Max(-5, value.X), Math.Max(0, value.Y));

                if (SetProperty(ref _viewportLocation, clampedValue))
                {
                    _layoutService.UpdateViewportY(clampedValue.Y, this);
                }
                else if (value != clampedValue)
                {
                    // 如果 UI 传来的非法值被我们无视了，
                    // 必须立刻反抽 UI 一巴掌（触发通知），强制 UI 滚回合法位置！
                    OnPropertyChanged(nameof(ViewportLocation));
                }

                UpdatePlayheadX();
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
        public ObservableCollection<TimeLineItemViewModelBase> LineBlocks { get; } = new();

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

            ViewportLocation = new Point(-5, 0);
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

            // 2. 遍历底层的每一根判定线
            foreach (var line in chart.JudgementLines)
            {
                // 先把判定线本身的蓝色图层塞进去
                var lineBlockVM = new TimeLineJudgmentLineViewModel(line, this, _layoutService);
                LineBlocks.Add(lineBlockVM);

                // 🌟 核心逻辑：扫描这根判定线所有的属性，把里面的关键帧提取出来变成菱形！
                if (line.Properties != null)
                {
                    // 提取 Position 关键帧
                    if (line.Properties.Position?.KeyFrames != null)
                    {
                        foreach (var kf in line.Properties.Position.KeyFrames)
                        {
                            LineBlocks.Add(new TimeLineKeyFrameViewModel(line, "Position", kf, this, _layoutService));
                        }
                    }

                    // 提取 Scale 关键帧
                    if (line.Properties.Scale?.KeyFrames != null)
                    {
                        foreach (var kf in line.Properties.Scale.KeyFrames)
                        {
                            LineBlocks.Add(new TimeLineKeyFrameViewModel(line, "Scale", kf, this, _layoutService));
                        }
                    }

                    // 提取 Rotation 关键帧
                    if (line.Properties.Rotation?.KeyFrames != null)
                    {
                        foreach (var kf in line.Properties.Rotation.KeyFrames)
                        {
                            LineBlocks.Add(new TimeLineKeyFrameViewModel(line, "Rotation", kf, this, _layoutService));
                        }
                    }

                    // 提取 Opacity 关键帧
                    if (line.Properties.Opacity?.KeyFrames != null)
                    {
                        foreach (var kf in line.Properties.Opacity.KeyFrames)
                        {
                            LineBlocks.Add(new TimeLineKeyFrameViewModel(line, "Opacity", kf, this, _layoutService));
                        }
                    }

                    // 提取 Speed 关键帧
                    if (line.Properties.Speed?.KeyFrames != null)
                    {
                        foreach (var kf in line.Properties.Speed.KeyFrames)
                        {
                            LineBlocks.Add(new TimeLineKeyFrameViewModel(line, "Speed", kf, this, _layoutService));
                        }
                    }
                }
            }


            
        }

        // === 当用户使用快捷键/鼠标滚轮改变缩放时 ===
        // === 当用户使用快捷键/鼠标滚轮改变缩放时 ===
        partial void OnZoomChanged(double value)
        {
            RefreshAllBlocksVisuals();
            UpdatePlayheadX(); // 🌟 缩放变了，游标位置也得重新算！
        }

        partial void OnPixelPerTickChanged(double value)
        {
            RefreshAllBlocksVisuals();
            UpdatePlayheadX(); // 🌟 基础比例变了，游标位置也得重算！
        }

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


        // === 游标状态 ===
        private int _currentTick = 0;
        public int CurrentTick
        {
            get => _currentTick;
            set
            {
                if (SetProperty(ref _currentTick, value))
                {
                    UpdatePlayheadX();
                }
            }
        }

        private double _playheadX;
        public double PlayheadX
        {
            get => _playheadX;
            private set => SetProperty(ref _playheadX, value);
        }

        // 🌟 只要时间、缩放、相机位置一变，游标立刻自动对齐！
        private void UpdatePlayheadX()
        {
            PlayheadX = (CurrentTick * PixelPerTick * Zoom) - ViewportLocation.X;
        }

        

        // 🌟 供游标拖拽时调用的绝对位移算法 (和关键帧一模一样！)
        public void MovePlayheadByAbsoluteDelta(double totalDeltaX, int startTick)
        {
            double pixelsPerTick = PixelPerTick * Zoom;
            double tickDeltaDouble = totalDeltaX / pixelsPerTick;

            // 四舍五入，保证完美吸附手感
            int tickDelta = (int)Math.Round(tickDeltaDouble, MidpointRounding.AwayFromZero);
            CurrentTick = Math.Max(0, startTick + tickDelta);
        }
    }
}