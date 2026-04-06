using Axphi.Data; // 替换成你 Chart 和 JudgementLine 所在的实际命名空间
using Axphi.Data.KeyFrames;
using Axphi.Services;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Axphi.ViewModels
{
    public enum TimelineSelectionContext
    {
        None,
        Layers,
        SubItems,
    }

    // 必须继承 ObservableObject 才能使用 MVVM 魔法
    public partial class TimelineViewModel : ObservableObject
    {
        // 【新增】保存全局数据源的引用
        private readonly IProjectSession _projectSession;
        private readonly ITimelineTrackFactory _trackFactory;
        private readonly ITimelineHistoryCoordinator _historyCoordinator;
        private readonly ITimelineEditingService _editingService;
        private readonly ITimelineSnapService _snapService;
        private readonly ITimelineClipboardService _clipboardService;
        private readonly ITimelineMutationSyncService _mutationSyncService;
        private readonly ITimelineStateService _stateService;
        private readonly ITimelineWorkspaceLoopService _workspaceLoopService;
        private readonly ITimelineSelectionService _selectionService;
        private readonly ITimelinePlaybackSyncService _playbackSyncService;
        private readonly IMessenger _messenger;
        private readonly List<KeyframeClipboardItem> _keyframeClipboard = new();
        private readonly List<JudgementLine> _judgementLineClipboard = new();
        private bool _isReloadingChartState;
        private bool _isApplyingHistorySnapshot;

        // 核心数据：需要暴露给界面的谱面对象
        [ObservableProperty]
        private Chart _currentChart = new Chart();

        // 1. 缩放比例 (Zoom)：相当于按住 Alt 滚轮修改的值，默认是 1.0
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPixelWidth))]
        [NotifyPropertyChangedFor(nameof(TimelineSurfaceWidth))]
        [NotifyPropertyChangedFor(nameof(MaxScrollOffset))]// 当 Zoom 改变时，通知界面重新获取总宽度
        private double _zoomScale = 1.0;

        // 2. 谱面总长度 (以 128分音符/Tick 为单位)
        // 假设一首 2 分钟的 120BPM 歌曲，大约有 240 拍 * 32 = 7680 个 Tick。我们先给个默认值 10000 够长了。
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPixelWidth))]
        [NotifyPropertyChangedFor(nameof(TimelineSurfaceWidth))]
        [NotifyPropertyChangedFor(nameof(MaxScrollOffset))]
        private int _totalDurationTicks = 10000;

        // 3. 基础缩放系数：1 个 Tick 默认占多少像素？
        // 如果给 1.0，那 10000 个 Tick 就是 10000 像素，太长了。我们默认给个 0.5 像素试试。
        private const double BasePixelsPerTick = 0.5;

        // 4. 【核心魔法】计算出右侧轨道的物理总像素宽度！UI 会绑定这个值！
        public double TotalPixelWidth => TotalDurationTicks * BasePixelsPerTick * ZoomScale;

        public double TimelineSurfaceWidth => TotalPixelWidth + RightEmptyPadding;

        // ================= 🌟 新增：滚动条防越界限制 =================
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaxScrollOffset))]
        private double _viewportActualWidth = 800; // 默认给个安全值

        [ObservableProperty]
        private double _currentHorizontalScrollOffset;

        // 🌟 新增：在最右侧强行留出一段安全空白（单位：像素）
        // 你可以根据喜好调整大小
        public double RightEmptyPadding => 15.0;


        // 计算真正的最大允许滚动距离（总宽 - 屏幕宽，最小为0防止缩放太小报错）
        public double MaxScrollOffset => Math.Max(0, TotalPixelWidth - ViewportActualWidth+ RightEmptyPadding);



        // === 游标核心属性 ===

        // 接收来自 ChartDisplay 的实时播放秒数
        [ObservableProperty]
        private double _currentPlayTimeSeconds;

        // 供 XAML 里红色游标绑定的 X 物理坐标
        [ObservableProperty]
        private double _playheadPositionX;

        // 1. 当播放器的时间改变时，重新计算游标位置
        partial void OnCurrentPlayTimeSecondsChanged(double value)
        {
            GetProjectMetadata().PlayheadTimeSeconds = value;
            UpdatePlayheadPosition();

            //  算出当前的 Tick
            int currentTick = GetCurrentTick();

            //  拿到当前谱面的缓动方向设置
            var easingDirection = CurrentChart.KeyFrameEasingDirection;

            _playbackSyncService.SyncTrackValuesToTime(currentTick, easingDirection, BpmTrack, Tracks);
        }

        // 2. 当你按 Alt+滚轮 缩放时，游标位置也必须跟着伸缩！
        partial void OnZoomScaleChanged(double value)
        {
            GetProjectMetadata().ZoomScale = value;

            // 注意：因为 TotalPixelWidth 用了 NotifyPropertyChangedFor
            // 它会自动更新，但我们必须手动调用更新游标
            UpdatePlayheadPosition();

            UpdateWorkspacePixels(); // 🌟 补上这句：缩放时也要更新工作区宽度


            // 告诉全网：缩放变了！所有的关键帧请重新计算你们的 X 坐标！
            _messenger.Send(new ZoomScaleChangedMessage(value));
        }
        // ================= 🌟 新增：当总时长被修改时，重新换算所有的进度比例 =================
        partial void OnTotalDurationTicksChanged(int value)
        {
            // 防御性编程：总时长不能小于或者等于 0
            if (value <= 100)
            {
                TotalDurationTicks = 100;
                return;
            }

            // 1. 确保工作区右边界不会越界（如果把总时长改短了）
            if (WorkspaceEndTick > value)
            {
                WorkspaceEndTick = value;
            }

            // 2. 刷新时间轴上的循环工作区物理像素
            UpdateWorkspacePixels();

            // 3. 刷新小地图里工作区和视野框的比例和物理像素
            UpdateMinimapPixels();

            GetProjectMetadata().TotalDurationTicks = value;

            if (!_isReloadingChartState)
            {
                CurrentChart.Duration = value;
                _messenger.Send(new JudgementLinesChangedMessage());
            }
        }

        partial void OnCurrentHorizontalScrollOffsetChanged(double value)
        {
            GetProjectMetadata().CurrentHorizontalScrollOffset = value;
        }


        // 核心换算公式
        private void UpdatePlayheadPosition()
        {
            

            // 1. 拿到积分器算出来的、绝对准确的当前 Tick！(取代了旧的乘除法)
            double currentTick = GetExactTick();

            // 2. 把 Tick 转换成屏幕上的像素 X 坐标！
            PlayheadPositionX = TickToPixel(currentTick);
        }


        // ✨ 全局唯一的 BPM 轨道！
        [ObservableProperty]
        private BpmTrackViewModel? _bpmTrack;


        // ✨ 全局唯一的 Audio 轨道！
        [ObservableProperty]
        private AudioTrackViewModel? _audioTrack;

        // ================= 新增：专供 UI 绑定的轨道视图模型集合 =================
        public ObservableCollection<TrackViewModel> Tracks { get; } = new ObservableCollection<TrackViewModel>();

        public NoteSelectionPanelViewModel NoteSelectionPanel { get; }
        public JudgementLineEditorViewModel JudgementLineEditor { get; }

        [ObservableProperty]
        private TrackViewModel? _activeNotePanelOwner;

        [ObservableProperty]
        private TimelineSelectionContext _activeSelectionContext = TimelineSelectionContext.None;
        


        // 构造函数：初始化时，可以先给个空谱面，或者由外部传进来
        public TimelineViewModel(
            IProjectSession projectSession,
            ITimelineTrackFactory trackFactory,
            ITimelineHistoryCoordinator historyCoordinator,
            ITimelineEditingService editingService,
            ITimelineSnapService snapService,
            ITimelineClipboardService clipboardService,
            ITimelineMutationSyncService mutationSyncService,
            ITimelineStateService stateService,
            ITimelineWorkspaceLoopService workspaceLoopService,
            ITimelineSelectionService selectionService,
            ITimelinePlaybackSyncService playbackSyncService,
            IMessenger messenger)
        {

            _projectSession = projectSession; // 存进私有变量
            _trackFactory = trackFactory;
            _historyCoordinator = historyCoordinator;
            _editingService = editingService;
            _snapService = snapService;
            _clipboardService = clipboardService;
            _mutationSyncService = mutationSyncService;
            _stateService = stateService;
            _workspaceLoopService = workspaceLoopService;
            _selectionService = selectionService;
            _playbackSyncService = playbackSyncService;
            _messenger = messenger;
            NoteSelectionPanel = new NoteSelectionPanelViewModel(this, _messenger);
            JudgementLineEditor = new JudgementLineEditorViewModel(this, _messenger);


            // 🌟 必须加上这行！让它一出生就计算宽度！
            UpdateWorkspacePixels();


            // 然后再从 manager 里把 Chart 拿出来赋值给 _currentChart
            // 极其重要的防坑提示：软件刚启动时，工程可能是空的！所以要做个判空！
            if (_projectSession.EditingProject != null)
            {
                ReloadTracksFromCurrentChart();
                ResetHistorySnapshot();
            }

            _messenger.Register<TimelineViewModel, JudgementLinesChangedMessage>(this, (recipient, message) =>
            {
                recipient.ScheduleHistorySnapshotCapture();
            });


            _messenger.Register<TimelineViewModel, ProjectLoadedMessage>(this, (recipient, message) =>
            {
                // 重新去抱 ProjectManager 的大腿！拿到最新的“谱面B”！
                if (recipient._projectSession.EditingProject != null)
                {
                    recipient._keyframeClipboard.Clear();
                    recipient.NotifyKeyframeClipboardCommandsStateChanged();
                    // 收到换工程的广播后，立刻执行重建动作！
                    recipient.ReloadTracksFromCurrentChart();
                    recipient.ResetHistorySnapshot();
                }
            });


        }

        public void ClearKeyframeSelection(object? senderToIgnore = null)
        {
            _editingService.ClearKeyframeSelection(CreateSelectionRuntime(), senderToIgnore);
        }

        public void ClearLayerSelection(object? senderToIgnore = null)
        {
            _editingService.ClearLayerSelection(CreateSelectionRuntime(), senderToIgnore);
        }

        public void ClearNoteSelection(object? senderToIgnore = null)
        {
            _editingService.ClearNoteSelection(CreateSelectionRuntime(), senderToIgnore);
        }

        public void ClearAllSelections()
        {
            _editingService.ClearAllSelections(CreateSelectionRuntime());
        }

        public void EnterLayerSelectionContext(object? senderToIgnore = null)
        {
            _editingService.EnterLayerSelectionContext(CreateSelectionRuntime(), senderToIgnore);
        }

        public void EnterSubItemSelectionContext(object? senderToIgnore = null)
        {
            _editingService.EnterSubItemSelectionContext(CreateSelectionRuntime(), senderToIgnore);
        }

        public bool IsTrackLevelKeyframeWrapperSelected(object wrapper)
        {
            return _selectionService.IsTrackLevelKeyframeWrapperSelected(Tracks, wrapper);
        }

        public int GetSelectedTrackLevelKeyframeCount()
        {
            return _selectionService.GetSelectedTrackLevelKeyframeCount(Tracks);
        }

        public void SetFreezeStateForSelectedTrackLevelKeyframes(bool isFreeze)
        {
            _selectionService.SetFreezeStateForSelectedTrackLevelKeyframes(Tracks, isFreeze);
        }

        public bool ApplyEasingToSelectedKeyframes(BezierEasing easing)
        {
            return _selectionService.ApplyEasingToSelectedKeyframes(BpmTrack, Tracks, easing);
        }

        public void RefreshNoteSelectionState(TrackViewModel? preferredOwner = null, NoteViewModel? preferredSingle = null)
        {
            ActiveNotePanelOwner = _selectionService.RefreshSelection(
                Tracks,
                ActiveNotePanelOwner,
                NoteSelectionPanel,
                preferredOwner,
                preferredSingle);
        }

        // 核心命令：点击“+添加判定线”时触发
        [RelayCommand]
        private void AddJudgementLine()
        {
            

            // 1. 实例化底层的纯净数据
            // 新建一条判定线
            var newLine = new JudgementLine();

            // 把新线加进集合，界面会自动更新！
            CurrentChart.JudgementLines.Add(newLine);
            // 发出消息
            NotifyJudgementLinesChanged();
            // 2. 实例化这个数据的“代理人”，并加进 UI 集合里！
            var newTrackVM = _trackFactory.CreateTrack(newLine, $"判定线图层 {Tracks.Count + 1}", this);
            Tracks.Add(newTrackVM);
            RefreshParentLineBindings();
        }

        // ================= 【新增的核心函数】 =================
        /// <summary>
        /// 根据当前 ProjectManager 里的真实谱面，重新生成左侧的所有 Track UI
        /// </summary>
        private void ReloadTracksFromCurrentChart(TimelineUiState? preservedUiState = null)
        {
            if (_projectSession.EditingProject == null || _projectSession.EditingProject.Chart == null)
                return;

            _isReloadingChartState = true;
            try
            {
                var metadata = GetProjectMetadata();

                if (preservedUiState != null)
                {
                    ViewportActualWidth = preservedUiState.ViewportActualWidth;
                }

                ZoomScale = metadata.ZoomScale;

                // 1. 换绑剧本：把指针指向最新的真实谱面
                CurrentChart = _projectSession.EditingProject.Chart;

                int projectTotalDurationTicks = metadata.TotalDurationTicks > 0
                    ? metadata.TotalDurationTicks
                    : CurrentChart.Duration;
                TotalDurationTicks = Math.Max(100, projectTotalDurationTicks);
                CurrentChart.Duration = TotalDurationTicks;

                // ================= ✨ 塞入第一步：实例化 BPM 轨道！ =================
                BpmTrack = _trackFactory.CreateBpmTrack(CurrentChart, this);
                // =====================================================================

                // ================= ✨ 新增：实例化 Audio 轨道！ =================
                AudioTrack = _trackFactory.CreateAudioTrack(CurrentChart, this, _projectSession);


                // 2. 砸碎旧舞台：清空前端的 Track UI 集合 (这一步让旧 UI 被 GC 回收)
                Tracks.Clear();
                ActiveNotePanelOwner = null;
                NoteSelectionPanel.SyncSelection();
                JudgementLineEditor.CloseCommand.Execute(null);
                NotifyKeyframeClipboardCommandsStateChanged();

                // 3. 请上新演员：遍历新谱面里的判定线，挨个给它们创建前端代理人
                var materializedTracks = _trackFactory.BuildTracks(CurrentChart, this);
                foreach (var track in materializedTracks)
                {
                    Tracks.Add(track);
                }

                RefreshParentLineBindings();

                if (preservedUiState != null)
                {
                    _stateService.RestoreUiState(preservedUiState, Tracks, JudgementLineEditor);
                }

                var playbackState = _stateService.ResolvePlaybackState(metadata);
                CurrentHorizontalScrollOffset = playbackState.CurrentHorizontalScrollOffset;
                WorkspaceStartTick = playbackState.WorkspaceStartTick;
                WorkspaceEndTick = playbackState.WorkspaceEndTick;
                CurrentPlayTimeSeconds = playbackState.CurrentPlayTimeSeconds;

                if (playbackState.ShouldForceSeek)
                {
                    ForceSeekPlayback(playbackState.CurrentPlayTimeSeconds);
                }

                UpdateWorkspacePixels();

                // 5. 顺便大喊一声，让右侧的渲染器也强制刷新一下画面！
                NotifyJudgementLinesChanged();
            }
            finally
            {
                _isReloadingChartState = false;
            }

        }

        // 在 TimelineViewModel.cs 里加上这个方法 (上一回合提过，确认一下你加上了)
        public int GetCurrentTick()
        {
            double absoluteTick = GetExactTick();
            return (int)Math.Round(absoluteTick, MidpointRounding.AwayFromZero);
            
        }

        // 在 TimelineViewModel 里加上这个公开的换算方法
        public double TickToPixel(double tick)
        {
            return tick * BasePixelsPerTick * ZoomScale;
        }

        public int AudioOffsetTicks
        {
            get => GetProjectMetadata().AudioOffsetTicks;
            set => GetProjectMetadata().AudioOffsetTicks = value;
        }

        public double AudioVolume
        {
            get => GetProjectMetadata().AudioVolume;
            set => GetProjectMetadata().AudioVolume = value;
        }

        // 像素 反推回 Tick
        public double PixelToTick(double pixelX)
        {
            return pixelX / (BasePixelsPerTick * ZoomScale);
        }
        // 1. 新增一个获取精确小数 Tick 的方法
        public double GetExactTick()
        {
            
            // 不做强制 int 转换，原汁原味返回精确小数
            double exactTick = TimeTickConverter.TimeToTick(CurrentPlayTimeSeconds, CurrentChart.BpmKeyFrames, CurrentChart.InitialBpm);
            //return exactTick + CurrentChart.Offset;
            return exactTick;
        }

        private ProjectMetadata GetProjectMetadata()
        {
            _projectSession.EditingProject ??= new Project();
            _projectSession.EditingProject.Metadata ??= new ProjectMetadata();
            return _projectSession.EditingProject.Metadata;
        }

        // ================= 核心命令：全局删除选中的关键帧 =================
        [RelayCommand]
        private void DeleteSelectedKeyframes()
        {
            if (_editingService.DeleteSelected(CreateDeleteRuntime()))
            {
                FinalizeDeleteChanges();
            }
        }

        public void RefreshLayerSelectionVisuals()
        {
            _editingService.RefreshLayerSelectionVisuals(CreateSelectionRuntime());
        }

        private TimelineSelectionRuntime CreateSelectionRuntime()
        {
            return new TimelineSelectionRuntime(
                Tracks,
                NoteSelectionPanel,
                context => ActiveSelectionContext = context,
                NotifyKeyframeClipboardCommandsStateChanged,
                (owner, preferredSingle) => RefreshNoteSelectionState(owner, preferredSingle),
                () => _selectionService.HasSelectedEditableKeyframes(BpmTrack, Tracks));
        }

        private TimelineDeleteRuntime CreateDeleteRuntime()
        {
            return new TimelineDeleteRuntime(
                CurrentChart,
                Tracks,
                BpmTrack,
                AudioTrack,
                context => ActiveSelectionContext = context,
                ReindexTrackNames,
                RefreshParentLineBindings);
        }

        private void FinalizeDeleteChanges()
        {
            RefreshLayerSelectionVisuals();
            NotifyKeyframeClipboardCommandsStateChanged();

            _mutationSyncService.SyncAfterMutation(CreateMutationRuntime(syncNotes: false, broadcastSortMessage: false));
        }

        private TimelineMutationRuntime CreateMutationRuntime(bool syncNotes, bool broadcastSortMessage)
        {
            return new TimelineMutationRuntime(
                _messenger,
                AudioTrack,
                BpmTrack,
                Tracks,
                GetCurrentTick(),
                CurrentChart.KeyFrameEasingDirection,
                broadcastSortMessage,
                syncNotes);
        }

        private void ReindexTrackNames()
        {
            _editingService.ReindexTrackNames(Tracks);
        }

        public bool TrySetParentLine(TrackViewModel childTrack, string? parentLineId)
        {
            return _editingService.TrySetParentLine(
                Tracks,
                childTrack,
                parentLineId,
                () => _messenger.Send(new JudgementLinesChangedMessage()));
        }

        private void RefreshParentLineBindings()
        {
            _editingService.RefreshParentLineBindings(
                Tracks,
                () => _messenger.Send(new JudgementLinesChangedMessage()));
        }



        // TimelineViewModel.cs 里新增：

        /// <summary>
        /// 智能磁吸算法：根据当前缩放比例，寻找最近的小节线或关键帧
        /// </summary>
        // 🌟 新增参数 isPlayhead，用来告诉雷达：“现在是不是游标正在移动？”
        public int SnapToClosest(double exactTickDouble, bool isPlayhead = false)
        {
            double pixelsPerTick = TickToPixel(1000) / 1000.0;
            int playheadTick = (int)Math.Round(PixelToTick(PlayheadPositionX));
            bool isSnapModifierActive = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift);

            return _snapService.ResolveSnappedTick(new TimelineSnapRuntime(
                isSnapModifierActive,
                exactTickDouble,
                isPlayhead,
                pixelsPerTick,
                playheadTick,
                BpmTrack,
                Tracks));
        }











        // ================= 工作区 (循环区间) =================
        [ObservableProperty]
        private int _workspaceStartTick = 0;

        [ObservableProperty]
        private int _workspaceEndTick = 1920; // 默认给个 1920 的长度先看着

        [ObservableProperty]
        private double _workspaceStartX;

        [ObservableProperty]
        private double _workspaceEndX;

        [ObservableProperty]
        private double _workspaceWidth;

        // 只要 Tick 改变，立刻重新计算像素
        partial void OnWorkspaceStartTickChanged(int value)
        {
            GetProjectMetadata().WorkspaceStartTick = value;
            UpdateWorkspacePixels();
        }

        partial void OnWorkspaceEndTickChanged(int value)
        {
            GetProjectMetadata().WorkspaceEndTick = value;
            UpdateWorkspacePixels();
        }

        public void UpdateWorkspacePixels()
        {
            WorkspaceStartX = TickToPixel(WorkspaceStartTick);
            WorkspaceEndX = TickToPixel(WorkspaceEndTick);
            WorkspaceWidth = Math.Max(0, WorkspaceEndX - WorkspaceStartX);
            UpdateMinimapPixels();
        }
        // ================= 工作区循环拦截引擎 =================
        // 供外部播放引擎在“正在播放”状态下每一帧调用
        // ================= 工作区循环拦截引擎 =================
        // 🌟 增加两个参数：接收上一帧的时间和当前帧的时间
        public void CheckWorkspaceLoop(double prevTimeSeconds, double currentTimeSeconds)
        {
            if (CurrentChart == null) return;

            var loopTargetSeconds = _workspaceLoopService.ResolveLoopTargetSeconds(
                CurrentChart,
                TotalDurationTicks,
                WorkspaceStartTick,
                WorkspaceEndTick,
                prevTimeSeconds,
                currentTimeSeconds);

            if (loopTargetSeconds is double startSeconds)
            {
                CurrentPlayTimeSeconds = startSeconds;
                ForceSeekPlayback(startSeconds);
                NotifyJudgementLinesChanged();
            }
        }

        private void NotifyJudgementLinesChanged()
        {
            _messenger.Send(new JudgementLinesChangedMessage());
        }

        private void ForceSeekPlayback(double targetSeconds)
        {
            _messenger.Send(new ForceSeekMessage(targetSeconds));
        }





        // ================= 全局缩略图 (Minimap / Viewport) =================

        // 当前屏幕左边缘对应的 Tick
        [ObservableProperty]
        private double _viewportStartTick;

        // 当前屏幕右边缘对应的 Tick
        [ObservableProperty]
        private double _viewportEndTick;

        // 缩略图所在区域的实际物理宽度（UI 传给我们的）
        [ObservableProperty]
        private double _minimapActualWidth = 1; // 给个默认值防 0 报错

        // ================= 换算给 UI 绑定的像素值 =================

        // 1. 工作区 (纯展示) 在缩略图里的 X 和 Width
        public double MinimapWorkspaceX => TotalDurationTicks == 0 ? 0 : (WorkspaceStartTick / (double)TotalDurationTicks) * MinimapActualWidth;
        public double MinimapWorkspaceWidth => TotalDurationTicks == 0 ? 0 : ((WorkspaceEndTick - WorkspaceStartTick) / (double)TotalDurationTicks) * MinimapActualWidth;

        // 2. 屏幕视野 (带手柄) 在缩略图里的 X 和 Width
        public double MinimapViewportX => TotalDurationTicks == 0 ? 0 : (ViewportStartTick / (double)TotalDurationTicks) * MinimapActualWidth;
        public double MinimapViewportWidth => TotalDurationTicks == 0 ? 0 : ((ViewportEndTick - ViewportStartTick) / (double)TotalDurationTicks) * MinimapActualWidth;

        // 当这些基础值改变时，通知 UI 更新缩略图的像素！
        partial void OnMinimapActualWidthChanged(double value) => UpdateMinimapPixels();
        partial void OnViewportStartTickChanged(double value) => UpdateMinimapPixels();
        partial void OnViewportEndTickChanged(double value) => UpdateMinimapPixels();

        // 在你原有的 OnWorkspaceStartTickChanged 和 OnWorkspaceEndTickChanged 里，也要加上 UpdateMinimapPixels() 的调用！

        public void UpdateMinimapPixels()
        {
            OnPropertyChanged(nameof(MinimapWorkspaceX));
            OnPropertyChanged(nameof(MinimapWorkspaceWidth));
            OnPropertyChanged(nameof(MinimapViewportX));
            OnPropertyChanged(nameof(MinimapViewportWidth));
        }




        // 🌟 新增：节拍器开关状态
        [ObservableProperty]
        private bool _isMetronomeEnabled = false;
    }



  
}
