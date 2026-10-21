using Axphi.Data;
using Axphi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using System.Windows;

namespace Axphi.ViewModels
{
    public partial class TimeLineViewModel : ObservableObject, IDisposable
    {


        // 必要服务
        private readonly ProjectManager _projectManager;
        private readonly TrackLayoutService _layoutService;
        private readonly PlaybackPositionService _playbackPositionService;


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

        

        public Chart? CurrentChart => _projectManager.EditingProject?.Chart;
        public double ContentHeight => _layoutService.ContentHeight;
        public ITrackLayoutProvider LayoutProvider => _layoutService;

        [ObservableProperty]
        private int _layoutVersion;

        public TimeLineViewModel(ProjectManager projectManager, TrackLayoutService layoutService, PlaybackPositionService playbackPositionService)
        {
            _projectManager = projectManager;
            _layoutService = layoutService;
            _playbackPositionService = playbackPositionService;


            // 监听服务的相机同步广播
            _layoutService.ViewportYChanged += OnViewportYChanged;

            // 监听布局服务，防止在缩放或调整 X 轴时，错过 Y 轴的变动（双保险）
            _layoutService.LayoutUpdated += OnLayoutUpdated;

            // 监听工程变更，确保谱面切换时能够同步刷新
            _projectManager.PropertyChanged += OnProjectManagerPropertyChanged;

            _playbackPositionService.PropertyChanged += OnPlaybackPositionPropertyChanged;

            ViewportLocation = new Point(-5, 0);
        }

        private void OnProjectManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_projectManager.EditingProject) || e.PropertyName == "Chart")
            {
                OnPropertyChanged(nameof(CurrentChart));
            }
        }

        private void OnViewportYChanged(object? sender, double newY)
        {
            // 如果广播是我自己发出的，或者 Y 根本没变，就不处理
            if (sender != this && Math.Abs(_viewportLocation.Y - newY) > 0.01)
            {
                // 替换 Y
                ViewportLocation = new Point(_viewportLocation.X, newY);
            }
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(ContentHeight));
            LayoutVersion++;
        }

        private void OnPlaybackPositionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlaybackPositionService.CurrentTick)
                && CurrentTick != _playbackPositionService.CurrentTick)
            {
                CurrentTick = _playbackPositionService.CurrentTick;
            }
        }

        public double GetLineYCoordinate(JudgementLine line)
        {
            return _layoutService.GetYCoordinate(line);
        }

        public double GetPropertyYCoordinate(JudgementLine line, string propertyName)
        {
            return _layoutService.GetPropertyYCoordinate(line, propertyName);
        }

        // 当用户使用快捷键/鼠标滚轮改变缩放时  
        partial void OnZoomChanged(double value)
        {
            UpdatePlayheadX(); // 缩放变了，游标位置也得重新算
        }


        // 实际上, 该函数不会被调用
        partial void OnPixelPerTickChanged(double value)
        {
            UpdatePlayheadX();
        }

        public void Dispose()
        {
            _layoutService.ViewportYChanged -= OnViewportYChanged;
            _projectManager.PropertyChanged -= OnProjectManagerPropertyChanged;
            _layoutService.LayoutUpdated -= OnLayoutUpdated;
            _playbackPositionService.PropertyChanged -= OnPlaybackPositionPropertyChanged;
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