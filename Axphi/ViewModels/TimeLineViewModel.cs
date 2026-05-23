using Axphi.Data;
using Axphi.Data.KeyFrames; // 确保引用了你的 IKeyFrame
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


        // 必要服务
        private readonly ProjectManager _projectManager;
        private readonly TrackLayoutService _layoutService;


        // 时间轴相机的位置
        private Point _viewportLocation;
        public Point ViewportLocation
        {
            get => _viewportLocation;
            set
            {
                // 钳制坐标值, 在这里钳制可以防止抖动
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


        // 视图参数
        [ObservableProperty]
        private double _pixelPerTick = 0.5; // 每 tick 0.5 像素, 一般情况下此值不会变更

        [ObservableProperty]
        private double _zoom = 1.0;

        

        // 给轨道的的 UI 列表
        public ObservableCollection<TimeLineItemViewModelBase> TimeLineElements { get; } = new();

        public TimeLineViewModel(ProjectManager projectManager, TrackLayoutService layoutService)
        {
            _projectManager = projectManager;
            _layoutService = layoutService;


            // 监听服务的相机同步广播
            _layoutService.ViewportYChanged += (sender, newY) =>
            {
                // 如果广播是我自己发出的，或者 Y 根本没变，就不处理
                if (sender != this && Math.Abs(_viewportLocation.Y - newY) > 0.01)
                {
                    // 替换 Y
                    ViewportLocation = new Point(_viewportLocation.X, newY);
                }
            };

            // 监听布局服务，防止在缩放或调整 X 轴时，错过 Y 轴的变动（双保险）
            _layoutService.LayoutUpdated += OnLayoutUpdated;

            // 监听工程变更，确保谱面切换时能够同步刷新
            _projectManager.PropertyChanged += OnProjectManagerPropertyChanged;

            RefreshElements();

            ViewportLocation = new Point(-5, 0);
        }

        private void OnProjectManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_projectManager.EditingProject) || e.PropertyName == "Chart")
            {
                RefreshElements();
            }
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            // 虽然每个子 Block 自己会监听 Y，但有时需要父级配合触发
            // 这里暂不执行，交给 TimeLineJudgmentLineViewModel 自己处理 Y 的变化。
        }

        private void RefreshElements()
        {
            // 清理旧资源，防止事件泄漏
            foreach (var block in TimeLineElements)
            {
                block.Dispose();
            }
            TimeLineElements.Clear();

            var chart = _projectManager.EditingProject?.Chart;
            if (chart?.JudgementLines == null) return;

            // 遍历底层的每一根判定线
            foreach (var line in chart.JudgementLines)
            {
                // 先把判定线本身的蓝色图层塞进去
                var lineBlockVM = new TimeLineJudgmentLineViewModel(line, this, _layoutService);
                TimeLineElements.Add(lineBlockVM);

                // 扫描这根判定线所有的属性，把里面的关键帧提取出来变成菱形
                if (line.Properties != null)
                {
                    // 提取 Position 关键帧
                    if (line.Properties.Position?.KeyFrames != null)
                    {
                        foreach (var kf in line.Properties.Position.KeyFrames)
                        {
                            TimeLineElements.Add(new TimeLineKeyFrameViewModel(line, "Position", kf, _layoutService, this.PixelPerTick, this.Zoom));
                        }
                    }

                    // 提取 Scale 关键帧
                    if (line.Properties.Scale?.KeyFrames != null)
                    {
                        foreach (var kf in line.Properties.Scale.KeyFrames)
                        {
                            TimeLineElements.Add(new TimeLineKeyFrameViewModel(line, "Scale", kf,  _layoutService, this.PixelPerTick, this.Zoom));
                        }
                    }

                    // 提取 Rotation 关键帧
                    if (line.Properties.Rotation?.KeyFrames != null)
                    {
                        foreach (var kf in line.Properties.Rotation.KeyFrames)
                        {
                            TimeLineElements.Add(new TimeLineKeyFrameViewModel(line, "Rotation", kf,  _layoutService, this.PixelPerTick, this.Zoom));
                        }
                    }

                    // 提取 Opacity 关键帧
                    if (line.Properties.Opacity?.KeyFrames != null)
                    {
                        foreach (var kf in line.Properties.Opacity.KeyFrames)
                        {
                            TimeLineElements.Add(new TimeLineKeyFrameViewModel(line, "Opacity", kf,  _layoutService, this.PixelPerTick, this.Zoom));
                        }
                    }

                    // 提取 Speed 关键帧
                    if (line.Properties.Speed?.KeyFrames != null)
                    {
                        foreach (var kf in line.Properties.Speed.KeyFrames)
                        {
                            TimeLineElements.Add(new TimeLineKeyFrameViewModel(line, "Speed", kf, _layoutService, this.PixelPerTick, this.Zoom));
                        }
                    }
                }
            }


            
        }

        // 当用户使用快捷键/鼠标滚轮改变缩放时  
        partial void OnZoomChanged(double value)
        {
            RefreshAllElementsVisuals();
            UpdatePlayheadX(); // 缩放变了，游标位置也得重新算
        }


        // 实际上, 该函数不会被调用
        partial void OnPixelPerTickChanged(double value)
        {
            RefreshAllElementsVisuals();
            UpdatePlayheadX();
        }

        private void RefreshAllElementsVisuals()
        {
            foreach (var ele in TimeLineElements)
            {

                // 先推送最新的参数值（如果子类需要）
                if (ele is TimeLineItemViewModelBase item)
                {
                    item.PixelPerTick = this.PixelPerTick;
                    item.Zoom = this.Zoom;
                }


                ele.UpdateVisuals();
            }
        }

        public void Dispose()
        {
            _projectManager.PropertyChanged -= OnProjectManagerPropertyChanged;
            _layoutService.LayoutUpdated -= OnLayoutUpdated;
        }


        // 游标状态
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

        // 只要时间、缩放、相机位置一变，游标立刻自动对齐
        private void UpdatePlayheadX()
        {
            PlayheadX = (CurrentTick * PixelPerTick * Zoom) - ViewportLocation.X;
        }

        

       
        
    }
}