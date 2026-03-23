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
using System.Windows.Threading;

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
        private sealed record TrackUiState(
            string TrackId,
            bool IsExpanded,
            bool IsNoteExpanded);

        private sealed record JudgementLineEditorUiState(
            string ActiveTrackId,
            string CurrentNoteKind,
            int HorizontalDivisions,
            double ViewZoom,
            double PanX,
            double PanY);

        private sealed record TimelineUiState(
            double CurrentPlayTimeSeconds,
            double ZoomScale,
            double ViewportActualWidth,
            int WorkspaceStartTick,
            int WorkspaceEndTick,
            IReadOnlyList<TrackUiState> Tracks,
            JudgementLineEditorUiState? Editor);

        private enum KeyframeClipboardTarget
        {
            Bpm,
            TrackOffset,
            TrackScale,
            TrackRotation,
            TrackOpacity,
            TrackSpeed,
            NoteOffset,
            NoteScale,
            NoteRotation,
            NoteOpacity,
            NoteKind,
        }

        private sealed record KeyframeClipboardItem(
            KeyframeClipboardTarget Target,
            object? Owner,
            int Time,
            object Value,
            BezierEasing Easing);

        // 【新增】保存全局数据源的引用
        private readonly ProjectManager _projectManager;
        private readonly List<KeyframeClipboardItem> _keyframeClipboard = new();
        private readonly SnapshotHistory<string> _history = new(200);
        private readonly DispatcherTimer _historyCommitTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        private bool _isReloadingChartState;
        private bool _isApplyingHistorySnapshot;

        private static readonly JsonSerializerOptions HistoryJsonSerializerOptions = new()
        {
            IncludeFields = true,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate
        };

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
            UpdatePlayheadPosition();

            //  算出当前的 Tick
            int currentTick = GetCurrentTick();

            //  拿到当前谱面的缓动方向设置
            var easingDirection = CurrentChart.KeyFrameEasingDirection;

            // ================= ✨ 塞入第二步：让 BPM 跟着播放器跑！ =================
            BpmTrack?.SyncValuesToTime(currentTick);
            // =======================================================================


            //  大点兵！让所有的轨道根据当前时间更新自己的数值面板！
            foreach (var track in Tracks)
            {
                track.SyncValuesToTime(currentTick, easingDirection);

                // 让该轨道下的所有音符也同步它们的属性数值（这样选中音符时，面板里的数字才会跟着时间轴动！）
                foreach (var note in track.UINotes)
                {
                    note.SyncValuesToTime(currentTick, easingDirection);
                }
            }
        }

        // 2. 当你按 Alt+滚轮 缩放时，游标位置也必须跟着伸缩！
        partial void OnZoomScaleChanged(double value)
        {
            // 注意：因为 TotalPixelWidth 用了 NotifyPropertyChangedFor
            // 它会自动更新，但我们必须手动调用更新游标
            UpdatePlayheadPosition();

            UpdateWorkspacePixels(); // 🌟 补上这句：缩放时也要更新工作区宽度


            // 告诉全网：缩放变了！所有的关键帧请重新计算你们的 X 坐标！
            WeakReferenceMessenger.Default.Send(new ZoomScaleChangedMessage(value));
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

            if (!_isReloadingChartState)
            {
                CurrentChart.Duration = value;
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
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
        public TimelineViewModel(ProjectManager projectManager)
        {

            _projectManager = projectManager; // 存进私有变量
            NoteSelectionPanel = new NoteSelectionPanelViewModel(this);
            JudgementLineEditor = new JudgementLineEditorViewModel(this);
            _historyCommitTimer.Tick += (_, _) => FlushPendingHistorySnapshot();


            // 🌟 必须加上这行！让它一出生就计算宽度！
            UpdateWorkspacePixels();


            // 然后再从 manager 里把 Chart 拿出来赋值给 _currentChart
            // 极其重要的防坑提示：软件刚启动时，工程可能是空的！所以要做个判空！
            if (_projectManager.EditingProject != null)
            {
                ReloadTracksFromCurrentChart();
                ResetHistorySnapshot();
            }

            WeakReferenceMessenger.Default.Register<TimelineViewModel, JudgementLinesChangedMessage>(this, (recipient, message) =>
            {
                recipient.ScheduleHistorySnapshotCapture();
            });


            WeakReferenceMessenger.Default.Register<TimelineViewModel, ProjectLoadedMessage>(this, (recipient, message) =>
            {
                // 重新去抱 ProjectManager 的大腿！拿到最新的“谱面B”！
                if (recipient._projectManager.EditingProject != null)
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
            WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Keyframes", senderToIgnore));
            RefreshLayerSelectionVisuals();
            NotifyKeyframeClipboardCommandsStateChanged();
        }

        public void ClearLayerSelection(object? senderToIgnore = null)
        {
            WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Layers", senderToIgnore));
            RefreshLayerSelectionVisuals();
        }

        public void ClearNoteSelection(object? senderToIgnore = null)
        {
            WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Notes", senderToIgnore));

            foreach (var track in Tracks)
            {
                track.SelectedNote = null;
            }

            RefreshNoteSelectionState();
            RefreshLayerSelectionVisuals();
        }

        public void ClearAllSelections()
        {
            ClearKeyframeSelection();
            ClearNoteSelection();
            ClearLayerSelection();
            ActiveSelectionContext = TimelineSelectionContext.None;
        }

        public void EnterLayerSelectionContext(object? senderToIgnore = null)
        {
            ClearKeyframeSelection();
            ClearNoteSelection();
            ActiveSelectionContext = TimelineSelectionContext.Layers;
        }

        public void EnterSubItemSelectionContext(object? senderToIgnore = null)
        {
            ClearLayerSelection();
            ActiveSelectionContext = TimelineSelectionContext.SubItems;
        }

        public void RefreshNoteSelectionState(TrackViewModel? preferredOwner = null, NoteViewModel? preferredSingle = null)
        {
            var selectedEntries = Tracks
                .SelectMany(track => track.UINotes.Where(note => note.IsSelected).Select(note => (track, note)))
                .ToList();

            foreach (var track in Tracks)
            {
                track.SelectedNote = null;
                track.IsNotePanelOwner = false;
            }

            if (selectedEntries.Count == 0)
            {
                if (preferredOwner != null)
                {
                    ActiveNotePanelOwner = preferredOwner;
                }

                if (ActiveNotePanelOwner != null)
                {
                    ActiveNotePanelOwner.IsNotePanelOwner = true;
                }

                NoteSelectionPanel.SyncSelection(Array.Empty<NoteViewModel>());
                return;
            }

            TrackViewModel ownerTrack;
            if (preferredOwner != null && selectedEntries.Any(entry => ReferenceEquals(entry.track, preferredOwner)))
            {
                ownerTrack = preferredOwner;
            }
            else if (ActiveNotePanelOwner != null && selectedEntries.Any(entry => ReferenceEquals(entry.track, ActiveNotePanelOwner)))
            {
                ownerTrack = ActiveNotePanelOwner;
            }
            else
            {
                ownerTrack = selectedEntries[0].track;
            }

            ActiveNotePanelOwner = ownerTrack;
            ownerTrack.IsNotePanelOwner = true;

            if (selectedEntries.Count == 1)
            {
                var selectedNote = preferredSingle != null && selectedEntries.Any(entry => ReferenceEquals(entry.note, preferredSingle))
                    ? preferredSingle
                    : selectedEntries[0].note;
                ownerTrack = selectedEntries.First(entry => ReferenceEquals(entry.note, selectedNote)).track;
                ActiveNotePanelOwner = ownerTrack;
                ownerTrack.IsNotePanelOwner = true;
                ownerTrack.SelectedNote = selectedNote;
            }

            NoteSelectionPanel.SyncSelection(selectedEntries.Select(entry => entry.note).ToList());
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
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            // 2. 实例化这个数据的“代理人”，并加进 UI 集合里！
            var newTrackVM = new TrackViewModel(newLine, $"判定线图层 {Tracks.Count + 1}",this);
            Tracks.Add(newTrackVM);
        }

        private bool CanUndo() => _history.HasPendingChanges || _history.CanUndo;

        private bool CanRedo() => _history.CanRedo;

        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            FlushPendingHistorySnapshot();
            if (!_history.TryUndo(out var snapshot))
            {
                return;
            }

            ApplyHistorySnapshot(snapshot);
            NotifyHistoryCommandsStateChanged();
        }

        [RelayCommand(CanExecute = nameof(CanRedo))]
        private void Redo()
        {
            FlushPendingHistorySnapshot();
            if (!_history.TryRedo(out var snapshot))
            {
                return;
            }

            ApplyHistorySnapshot(snapshot);
            NotifyHistoryCommandsStateChanged();
        }

        // ================= 【新增的核心函数】 =================
        /// <summary>
        /// 根据当前 ProjectManager 里的真实谱面，重新生成左侧的所有 Track UI
        /// </summary>
        private void ReloadTracksFromCurrentChart(TimelineUiState? preservedUiState = null)
        {
            if (_projectManager.EditingProject == null || _projectManager.EditingProject.Chart == null)
                return;

            _isReloadingChartState = true;
            try
            {
                if (preservedUiState != null)
                {
                    ZoomScale = preservedUiState.ZoomScale;
                    ViewportActualWidth = preservedUiState.ViewportActualWidth;
                }

                // 1. 换绑剧本：把指针指向最新的真实谱面
                CurrentChart = _projectManager.EditingProject.Chart;
                if (CurrentChart.Duration > 0)
                {
                    TotalDurationTicks = Math.Max(100, CurrentChart.Duration);
                }

                // ================= ✨ 塞入第一步：实例化 BPM 轨道！ =================
                BpmTrack = new BpmTrackViewModel(CurrentChart, this);
                // =====================================================================

                // ================= ✨ 新增：实例化 Audio 轨道！ =================
                AudioTrack = new AudioTrackViewModel(CurrentChart, this, _projectManager);


                // 2. 砸碎旧舞台：清空前端的 Track UI 集合 (这一步让旧 UI 被 GC 回收)
                Tracks.Clear();
                ActiveNotePanelOwner = null;
                NoteSelectionPanel.SyncSelection(Array.Empty<NoteViewModel>());
                JudgementLineEditor.CloseCommand.Execute(null);
                NotifyKeyframeClipboardCommandsStateChanged();

                // 3. 请上新演员：遍历新谱面里的判定线，挨个给它们创建前端代理人
                if (CurrentChart.JudgementLines != null)
                {
                    for (int i = 0; i < CurrentChart.JudgementLines.Count; i++)
                    {
                        var line = CurrentChart.JudgementLines[i];
                        // 名字自动按序号排：判定线图层 1, 判定线图层 2...
                        var newTrackVM = new TrackViewModel(line, $"判定线图层 {i + 1}",this);
                        Tracks.Add(newTrackVM);
                    }
                }

                if (preservedUiState != null)
                {
                    var trackStatesById = preservedUiState.Tracks.ToDictionary(track => track.TrackId);
                    foreach (var track in Tracks)
                    {
                        if (trackStatesById.TryGetValue(track.Data.ID, out var trackState))
                        {
                            track.IsExpanded = trackState.IsExpanded;
                            track.IsNoteExpanded = trackState.IsNoteExpanded;
                        }
                    }
                }

                if (preservedUiState != null)
                {
                    WorkspaceStartTick = preservedUiState.WorkspaceStartTick;
                    WorkspaceEndTick = preservedUiState.WorkspaceEndTick;
                    CurrentPlayTimeSeconds = preservedUiState.CurrentPlayTimeSeconds;
                }
                else
                {
                    // 4. (可选) 让时间轴游标归零
                    CurrentPlayTimeSeconds = 0;

                    // 🌟 【新增核心修复】：同时发信给右侧渲染器和音频播放器，强制它们也空降回 0 秒！
                    // 这样前后端的记忆就彻底统一了！
                    WeakReferenceMessenger.Default.Send(new ForceSeekMessage(0));
                }

                UpdateWorkspacePixels();

                if (preservedUiState?.Editor is { } editorState)
                {
                    var targetTrack = Tracks.FirstOrDefault(track => track.Data.ID == editorState.ActiveTrackId);
                    if (targetTrack != null)
                    {
                        JudgementLineEditor.Open(targetTrack);
                        JudgementLineEditor.CurrentNoteKind = editorState.CurrentNoteKind;
                        JudgementLineEditor.HorizontalDivisions = editorState.HorizontalDivisions;
                        JudgementLineEditor.ViewZoom = editorState.ViewZoom;
                        JudgementLineEditor.PanX = editorState.PanX;
                        JudgementLineEditor.PanY = editorState.PanY;
                    }
                }

                if (preservedUiState != null)
                {
                    WeakReferenceMessenger.Default.Send(new ForceSeekMessage(preservedUiState.CurrentPlayTimeSeconds));
                }

                // 5. 顺便大喊一声，让右侧的渲染器也强制刷新一下画面！
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            finally
            {
                _isReloadingChartState = false;
            }

        }

        private void ScheduleHistorySnapshotCapture()
        {
            if (_isReloadingChartState || _isApplyingHistorySnapshot || _projectManager.EditingProject?.Chart == null)
            {
                return;
            }

            _history.ObserveSnapshot(SerializeHistorySnapshot());
            if (_history.HasPendingChanges)
            {
                _historyCommitTimer.Stop();
                _historyCommitTimer.Start();
                NotifyHistoryCommandsStateChanged();
            }
        }

        private void FlushPendingHistorySnapshot()
        {
            _historyCommitTimer.Stop();
            _history.FlushPendingChanges();
            NotifyHistoryCommandsStateChanged();
        }

        private void ResetHistorySnapshot()
        {
            _historyCommitTimer.Stop();
            _history.Reset(SerializeHistorySnapshot());
            NotifyHistoryCommandsStateChanged();
        }

        private string SerializeHistorySnapshot()
        {
            return JsonSerializer.Serialize(CurrentChart, HistoryJsonSerializerOptions);
        }

        private Chart DeserializeHistorySnapshot(string snapshot)
        {
            return JsonSerializer.Deserialize<Chart>(snapshot, HistoryJsonSerializerOptions) ?? new Chart();
        }

        private TimelineUiState CaptureTimelineUiState()
        {
            var trackStates = Tracks
                .Select(track => new TrackUiState(track.Data.ID, track.IsExpanded, track.IsNoteExpanded))
                .ToList();

            JudgementLineEditorUiState? editorState = null;
            if (JudgementLineEditor.ActiveTrack != null)
            {
                editorState = new JudgementLineEditorUiState(
                    JudgementLineEditor.ActiveTrack.Data.ID,
                    JudgementLineEditor.CurrentNoteKind,
                    JudgementLineEditor.HorizontalDivisions,
                    JudgementLineEditor.ViewZoom,
                    JudgementLineEditor.PanX,
                    JudgementLineEditor.PanY);
            }

            return new TimelineUiState(
                CurrentPlayTimeSeconds,
                ZoomScale,
                ViewportActualWidth,
                WorkspaceStartTick,
                WorkspaceEndTick,
                trackStates,
                editorState);
        }

        private void ApplyHistorySnapshot(string snapshot)
        {
            if (_projectManager.EditingProject == null)
            {
                return;
            }

            var uiState = CaptureTimelineUiState();
            var restoredChart = DeserializeHistorySnapshot(snapshot);
            var currentProject = _projectManager.EditingProject;

            _historyCommitTimer.Stop();
            _isApplyingHistorySnapshot = true;
            try
            {
                _projectManager.EditingProject = new Project
                {
                    Chart = restoredChart,
                    EncodedAudio = currentProject.EncodedAudio,
                    EncodedIllustration = currentProject.EncodedIllustration
                };

                ReloadTracksFromCurrentChart(uiState);
            }
            finally
            {
                _isApplyingHistorySnapshot = false;
            }
        }

        private void NotifyHistoryCommandsStateChanged()
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
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

        public void NotifyKeyframeClipboardCommandsStateChanged()
        {
            CopySelectedKeyframesCommand.NotifyCanExecuteChanged();
            PasteCopiedKeyframesCommand.NotifyCanExecuteChanged();
        }

        private int GetSelectedKeyframeCount()
        {
            int count = 0;

            if (BpmTrack != null)
            {
                count += BpmTrack.UIBpmKeyframes.Count(k => k.IsSelected);
            }

            foreach (var track in Tracks)
            {
                count += track.UIOffsetKeyframes.Count(k => k.IsSelected);
                count += track.UIScaleKeyframes.Count(k => k.IsSelected);
                count += track.UIRotationKeyframes.Count(k => k.IsSelected);
                count += track.UIOpacityKeyframes.Count(k => k.IsSelected);
                count += track.UISpeedKeyframes.Count(k => k.IsSelected);

                foreach (var note in track.UINotes)
                {
                    count += note.UIOffsetKeyframes.Count(k => k.IsSelected);
                    count += note.UIScaleKeyframes.Count(k => k.IsSelected);
                    count += note.UIRotationKeyframes.Count(k => k.IsSelected);
                    count += note.UIOpacityKeyframes.Count(k => k.IsSelected);
                    count += note.UINoteKindKeyframes.Count(k => k.IsSelected);
                }
            }

            return count;
        }

        private bool CanCopySelectedKeyframes() => GetSelectedKeyframeCount() > 0;

        private bool CanPasteCopiedKeyframes() => _keyframeClipboard.Count > 0;

        [RelayCommand(CanExecute = nameof(CanCopySelectedKeyframes))]
        private void CopySelectedKeyframes()
        {
            _keyframeClipboard.Clear();

            if (BpmTrack != null)
            {
                foreach (var wrapper in BpmTrack.UIBpmKeyframes.Where(k => k.IsSelected))
                {
                    _keyframeClipboard.Add(new KeyframeClipboardItem(KeyframeClipboardTarget.Bpm, null, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing));
                }
            }

            foreach (var track in Tracks)
            {
                foreach (var wrapper in track.UIOffsetKeyframes.Where(k => k.IsSelected))
                    _keyframeClipboard.Add(new KeyframeClipboardItem(KeyframeClipboardTarget.TrackOffset, track, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing));

                foreach (var wrapper in track.UIScaleKeyframes.Where(k => k.IsSelected))
                    _keyframeClipboard.Add(new KeyframeClipboardItem(KeyframeClipboardTarget.TrackScale, track, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing));

                foreach (var wrapper in track.UIRotationKeyframes.Where(k => k.IsSelected))
                    _keyframeClipboard.Add(new KeyframeClipboardItem(KeyframeClipboardTarget.TrackRotation, track, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing));

                foreach (var wrapper in track.UIOpacityKeyframes.Where(k => k.IsSelected))
                    _keyframeClipboard.Add(new KeyframeClipboardItem(KeyframeClipboardTarget.TrackOpacity, track, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing));

                foreach (var wrapper in track.UISpeedKeyframes.Where(k => k.IsSelected))
                    _keyframeClipboard.Add(new KeyframeClipboardItem(KeyframeClipboardTarget.TrackSpeed, track, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing));

                foreach (var note in track.UINotes)
                {
                    foreach (var wrapper in note.UIOffsetKeyframes.Where(k => k.IsSelected))
                        _keyframeClipboard.Add(new KeyframeClipboardItem(KeyframeClipboardTarget.NoteOffset, note, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing));

                    foreach (var wrapper in note.UIScaleKeyframes.Where(k => k.IsSelected))
                        _keyframeClipboard.Add(new KeyframeClipboardItem(KeyframeClipboardTarget.NoteScale, note, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing));

                    foreach (var wrapper in note.UIRotationKeyframes.Where(k => k.IsSelected))
                        _keyframeClipboard.Add(new KeyframeClipboardItem(KeyframeClipboardTarget.NoteRotation, note, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing));

                    foreach (var wrapper in note.UIOpacityKeyframes.Where(k => k.IsSelected))
                        _keyframeClipboard.Add(new KeyframeClipboardItem(KeyframeClipboardTarget.NoteOpacity, note, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing));

                    foreach (var wrapper in note.UINoteKindKeyframes.Where(k => k.IsSelected))
                        _keyframeClipboard.Add(new KeyframeClipboardItem(KeyframeClipboardTarget.NoteKind, note, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing));
                }
            }

            NotifyKeyframeClipboardCommandsStateChanged();
        }

        [RelayCommand(CanExecute = nameof(CanPasteCopiedKeyframes))]
        private void PasteCopiedKeyframes()
        {
            if (_keyframeClipboard.Count == 0)
            {
                return;
            }

            int earliestTime = _keyframeClipboard.Min(item => item.Time);
            int cursorTick = GetCurrentTick();
            int deltaTick = cursorTick - earliestTime;

            EnterSubItemSelectionContext();
            ClearKeyframeSelection();

            var pastedWrappers = new List<object>();
            foreach (var item in _keyframeClipboard.OrderBy(item => item.Time))
            {
                object? pastedWrapper = PasteClipboardItem(item, item.Time + deltaTick);
                if (pastedWrapper != null)
                {
                    pastedWrappers.Add(pastedWrapper);
                }
            }

            foreach (var wrapper in pastedWrappers)
            {
                switch (wrapper)
                {
                    case KeyFrameUIWrapper<double> doubleWrapper:
                        doubleWrapper.IsSelected = true;
                        break;
                    case KeyFrameUIWrapper<System.Windows.Vector> vectorWrapper:
                        vectorWrapper.IsSelected = true;
                        break;
                    case KeyFrameUIWrapper<NoteKind> kindWrapper:
                        kindWrapper.IsSelected = true;
                        break;
                }
            }

            RefreshLayerSelectionVisuals();
            NotifyKeyframeClipboardCommandsStateChanged();
            WeakReferenceMessenger.Default.Send(new KeyframesNeedSortMessage());
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            AudioTrack?.UpdatePixels();

            int currentTick = GetCurrentTick();
            var easingDirection = CurrentChart.KeyFrameEasingDirection;
            BpmTrack?.SyncValuesToTime(currentTick);
            foreach (var track in Tracks)
            {
                track.SyncValuesToTime(currentTick, easingDirection);
                foreach (var note in track.UINotes)
                {
                    note.SyncValuesToTime(currentTick, easingDirection);
                }
            }
        }

        private object? PasteClipboardItem(KeyframeClipboardItem item, int targetTime)
        {
            return item.Target switch
            {
                KeyframeClipboardTarget.Bpm => PasteBpmKeyframe(targetTime, (double)item.Value, item.Easing),
                KeyframeClipboardTarget.TrackOffset when item.Owner is TrackViewModel track => PasteTrackOffsetKeyframe(track, targetTime, (System.Windows.Vector)item.Value, item.Easing),
                KeyframeClipboardTarget.TrackScale when item.Owner is TrackViewModel track => PasteTrackScaleKeyframe(track, targetTime, (System.Windows.Vector)item.Value, item.Easing),
                KeyframeClipboardTarget.TrackRotation when item.Owner is TrackViewModel track => PasteTrackRotationKeyframe(track, targetTime, (double)item.Value, item.Easing),
                KeyframeClipboardTarget.TrackOpacity when item.Owner is TrackViewModel track => PasteTrackOpacityKeyframe(track, targetTime, (double)item.Value, item.Easing),
                KeyframeClipboardTarget.TrackSpeed when item.Owner is TrackViewModel track => PasteTrackSpeedKeyframe(track, targetTime, (double)item.Value, item.Easing),
                KeyframeClipboardTarget.NoteOffset when item.Owner is NoteViewModel note => PasteNoteOffsetKeyframe(note, targetTime, (System.Windows.Vector)item.Value, item.Easing),
                KeyframeClipboardTarget.NoteScale when item.Owner is NoteViewModel note => PasteNoteScaleKeyframe(note, targetTime, (System.Windows.Vector)item.Value, item.Easing),
                KeyframeClipboardTarget.NoteRotation when item.Owner is NoteViewModel note => PasteNoteRotationKeyframe(note, targetTime, (double)item.Value, item.Easing),
                KeyframeClipboardTarget.NoteOpacity when item.Owner is NoteViewModel note => PasteNoteOpacityKeyframe(note, targetTime, (double)item.Value, item.Easing),
                KeyframeClipboardTarget.NoteKind when item.Owner is NoteViewModel note => PasteNoteKindKeyframe(note, targetTime, (NoteKind)item.Value, item.Easing),
                _ => null,
            };
        }

        private KeyFrameUIWrapper<double>? PasteBpmKeyframe(int targetTime, double value, BezierEasing easing)
        {
            if (BpmTrack == null)
            {
                return null;
            }

            return UpsertKeyframe(CurrentChart.BpmKeyFrames, BpmTrack.UIBpmKeyframes, new KeyFrame<double>
            {
                Time = targetTime,
                Value = value,
                Easing = easing,
            });
        }

        private KeyFrameUIWrapper<System.Windows.Vector>? PasteTrackOffsetKeyframe(TrackViewModel track, int targetTime, System.Windows.Vector value, BezierEasing easing)
        {
            if (!Tracks.Contains(track)) return null;
            return UpsertKeyframe(track.Data.AnimatableProperties.Offset.KeyFrames, track.UIOffsetKeyframes, new OffsetKeyFrame { Time = targetTime, Value = value, Easing = easing });
        }

        private KeyFrameUIWrapper<System.Windows.Vector>? PasteTrackScaleKeyframe(TrackViewModel track, int targetTime, System.Windows.Vector value, BezierEasing easing)
        {
            if (!Tracks.Contains(track)) return null;
            return UpsertKeyframe(track.Data.AnimatableProperties.Scale.KeyFrames, track.UIScaleKeyframes, new ScaleKeyFrame { Time = targetTime, Value = value, Easing = easing });
        }

        private KeyFrameUIWrapper<double>? PasteTrackRotationKeyframe(TrackViewModel track, int targetTime, double value, BezierEasing easing)
        {
            if (!Tracks.Contains(track)) return null;
            return UpsertKeyframe(track.Data.AnimatableProperties.Rotation.KeyFrames, track.UIRotationKeyframes, new RotationKeyFrame { Time = targetTime, Value = value, Easing = easing });
        }

        private KeyFrameUIWrapper<double>? PasteTrackOpacityKeyframe(TrackViewModel track, int targetTime, double value, BezierEasing easing)
        {
            if (!Tracks.Contains(track)) return null;
            return UpsertKeyframe(track.Data.AnimatableProperties.Opacity.KeyFrames, track.UIOpacityKeyframes, new OpacityKeyFrame { Time = targetTime, Value = value, Easing = easing });
        }

        private KeyFrameUIWrapper<double>? PasteTrackSpeedKeyframe(TrackViewModel track, int targetTime, double value, BezierEasing easing)
        {
            if (!Tracks.Contains(track)) return null;
            return UpsertKeyframe(track.Data.SpeedKeyFrames, track.UISpeedKeyframes, new KeyFrame<double> { Time = targetTime, Value = value, Easing = easing });
        }

        private KeyFrameUIWrapper<System.Windows.Vector>? PasteNoteOffsetKeyframe(NoteViewModel note, int targetTime, System.Windows.Vector value, BezierEasing easing)
        {
            if (!note.ParentTrack.UINotes.Contains(note)) return null;
            return UpsertKeyframe(note.Model.AnimatableProperties.Offset.KeyFrames, note.UIOffsetKeyframes, new OffsetKeyFrame { Time = targetTime, Value = value, Easing = easing });
        }

        private KeyFrameUIWrapper<System.Windows.Vector>? PasteNoteScaleKeyframe(NoteViewModel note, int targetTime, System.Windows.Vector value, BezierEasing easing)
        {
            if (!note.ParentTrack.UINotes.Contains(note)) return null;
            return UpsertKeyframe(note.Model.AnimatableProperties.Scale.KeyFrames, note.UIScaleKeyframes, new ScaleKeyFrame { Time = targetTime, Value = value, Easing = easing });
        }

        private KeyFrameUIWrapper<double>? PasteNoteRotationKeyframe(NoteViewModel note, int targetTime, double value, BezierEasing easing)
        {
            if (!note.ParentTrack.UINotes.Contains(note)) return null;
            return UpsertKeyframe(note.Model.AnimatableProperties.Rotation.KeyFrames, note.UIRotationKeyframes, new RotationKeyFrame { Time = targetTime, Value = value, Easing = easing });
        }

        private KeyFrameUIWrapper<double>? PasteNoteOpacityKeyframe(NoteViewModel note, int targetTime, double value, BezierEasing easing)
        {
            if (!note.ParentTrack.UINotes.Contains(note)) return null;
            return UpsertKeyframe(note.Model.AnimatableProperties.Opacity.KeyFrames, note.UIOpacityKeyframes, new OpacityKeyFrame { Time = targetTime, Value = value, Easing = easing });
        }

        private KeyFrameUIWrapper<NoteKind>? PasteNoteKindKeyframe(NoteViewModel note, int targetTime, NoteKind value, BezierEasing easing)
        {
            if (!note.ParentTrack.UINotes.Contains(note)) return null;
            return UpsertKeyframe(note.Model.KindKeyFrames, note.UINoteKindKeyframes, new NoteKindKeyFrame { Time = targetTime, Value = value, Easing = easing });
        }

        private KeyFrameUIWrapper<T> UpsertKeyframe<T, TKeyFrame>(List<TKeyFrame> dataList, ObservableCollection<KeyFrameUIWrapper<T>> uiList, TKeyFrame frame)
            where T : struct
            where TKeyFrame : KeyFrame<T>
        {
            var existingWrapper = uiList.FirstOrDefault(wrapper => wrapper.Model.Time == frame.Time);
            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = frame.Value;
                existingWrapper.Model.Easing = frame.Easing;
                return existingWrapper;
            }

            dataList.Add(frame);
            dataList.Sort((a, b) => a.Time.CompareTo(b.Time));

            var newWrapper = new KeyFrameUIWrapper<T>(frame, this);
            uiList.Add(newWrapper);
            return newWrapper;
        }

        // ================= 核心命令：全局删除选中的关键帧 =================
        [RelayCommand]
        private void DeleteSelectedKeyframes()
        {
            var layersToSelectAfterDelete = new HashSet<TrackViewModel>();
            bool hasDeletedChildren = false;

            // 1. 扫荡全局 BPM 轨道
            if (BpmTrack != null)
            {
                // 找出所有 IsSelected == true 的保镖
                var bpmToDelete = BpmTrack.UIBpmKeyframes.Where(k => k.IsSelected).ToList();
                foreach (var wrapper in bpmToDelete)
                {
                    // 把底层数据和 UI 保镖一起做掉
                    CurrentChart.BpmKeyFrames.Remove(wrapper.Model);
                    BpmTrack.UIBpmKeyframes.Remove(wrapper);
                    hasDeletedChildren = true;
                }
            }

            // 2. 扫荡所有判定线图层，以及里面的音符！
            foreach (var track in Tracks)
            {
                // ================= A. 删判定线自己的关键帧 =================
                // Position (Offset)
                var offsetToDelete = track.UIOffsetKeyframes.Where(k => k.IsSelected).ToList();
                foreach (var wrapper in offsetToDelete)
                {
                    track.Data.AnimatableProperties.Offset.KeyFrames.Remove((OffsetKeyFrame)wrapper.Model);
                    track.UIOffsetKeyframes.Remove(wrapper);
                    hasDeletedChildren = true;
                    layersToSelectAfterDelete.Add(track);
                }
                // Scale
                var scaleToDelete = track.UIScaleKeyframes.Where(k => k.IsSelected).ToList();
                foreach (var wrapper in scaleToDelete)
                {
                    track.Data.AnimatableProperties.Scale.KeyFrames.Remove((ScaleKeyFrame)wrapper.Model);
                    track.UIScaleKeyframes.Remove(wrapper);
                    hasDeletedChildren = true;
                    layersToSelectAfterDelete.Add(track);
                }
                // Rotation
                var rotationToDelete = track.UIRotationKeyframes.Where(k => k.IsSelected).ToList();
                foreach (var wrapper in rotationToDelete)
                {
                    track.Data.AnimatableProperties.Rotation.KeyFrames.Remove((RotationKeyFrame)wrapper.Model);
                    track.UIRotationKeyframes.Remove(wrapper);
                    hasDeletedChildren = true;
                    layersToSelectAfterDelete.Add(track);
                }
                // Opacity
                var opacityToDelete = track.UIOpacityKeyframes.Where(k => k.IsSelected).ToList();
                foreach (var wrapper in opacityToDelete)
                {
                    track.Data.AnimatableProperties.Opacity.KeyFrames.Remove((OpacityKeyFrame)wrapper.Model);
                    track.UIOpacityKeyframes.Remove(wrapper);
                    hasDeletedChildren = true;
                    layersToSelectAfterDelete.Add(track);
                }

                var speedToDelete = track.UISpeedKeyframes.Where(k => k.IsSelected).ToList();
                foreach (var wrapper in speedToDelete)
                {
                    track.Data.SpeedKeyFrames.Remove(wrapper.Model);
                    track.UISpeedKeyframes.Remove(wrapper);
                    hasDeletedChildren = true;
                    layersToSelectAfterDelete.Add(track);
                }

                // ================= B. 删音符自己的关键帧 =================
                foreach (var note in track.UINotes)
                {
                    // Note Offset
                    var noteOffsetDel = note.UIOffsetKeyframes.Where(k => k.IsSelected).ToList();
                    foreach (var wrapper in noteOffsetDel) { note.Model.AnimatableProperties.Offset.KeyFrames.Remove((OffsetKeyFrame)wrapper.Model); note.UIOffsetKeyframes.Remove(wrapper); hasDeletedChildren = true; layersToSelectAfterDelete.Add(track); }

                    // Note Scale
                    var noteScaleDel = note.UIScaleKeyframes.Where(k => k.IsSelected).ToList();
                    foreach (var wrapper in noteScaleDel) { note.Model.AnimatableProperties.Scale.KeyFrames.Remove((ScaleKeyFrame)wrapper.Model); note.UIScaleKeyframes.Remove(wrapper); hasDeletedChildren = true; layersToSelectAfterDelete.Add(track); }

                    // Note Rotation
                    var noteRotDel = note.UIRotationKeyframes.Where(k => k.IsSelected).ToList();
                    foreach (var wrapper in noteRotDel) { note.Model.AnimatableProperties.Rotation.KeyFrames.Remove((RotationKeyFrame)wrapper.Model); note.UIRotationKeyframes.Remove(wrapper); hasDeletedChildren = true; layersToSelectAfterDelete.Add(track); }

                    // Note Opacity
                    var noteOpaDel = note.UIOpacityKeyframes.Where(k => k.IsSelected).ToList();
                    foreach (var wrapper in noteOpaDel) { note.Model.AnimatableProperties.Opacity.KeyFrames.Remove((OpacityKeyFrame)wrapper.Model); note.UIOpacityKeyframes.Remove(wrapper); hasDeletedChildren = true; layersToSelectAfterDelete.Add(track); }

                    // ✨ 新增：Note Kind 关键帧删除
                    if (note.Model.KindKeyFrames != null)
                    {
                        var noteKindDel = note.UINoteKindKeyframes.Where(k => k.IsSelected).ToList();
                        foreach (var wrapper in noteKindDel)
                        {
                            note.Model.KindKeyFrames.Remove((NoteKindKeyFrame)wrapper.Model);
                            note.UINoteKindKeyframes.Remove(wrapper);
                            hasDeletedChildren = true;
                            layersToSelectAfterDelete.Add(track);
                        }
                    }
                }

                

                // ================= C. 直接删掉被选中的音符本体！ =================
                var notesToDelete = track.UINotes.Where(n => n.IsSelected).ToList();
                foreach (var note in notesToDelete)
                {
                    track.Data.Notes.Remove(note.Model);
                    track.UINotes.Remove(note);
                    hasDeletedChildren = true;
                    layersToSelectAfterDelete.Add(track);

                    // 安全防护：如果删掉的正好是正在属性面板显示的那个音符，把面板清空
                    if (track.SelectedNote == note)
                    {
                        track.SelectedNote = null;
                    }
                }
            }

            if (hasDeletedChildren)
            {
                foreach (var track in layersToSelectAfterDelete)
                {
                    track.IsLayerSelected = true;
                }

                ActiveSelectionContext = TimelineSelectionContext.Layers;

                FinalizeDeleteChanges();
                return;
            }

            bool hasDeletedLayers = false;

            if (AudioTrack?.IsLayerSelected == true)
            {
                AudioTrack.DeleteAudio();
                hasDeletedLayers = true;
            }

            var tracksToDelete = Tracks.Where(track => track.IsLayerSelected).ToList();
            foreach (var track in tracksToDelete)
            {
                CurrentChart.JudgementLines.Remove(track.Data);
                Tracks.Remove(track);
                hasDeletedLayers = true;
            }

            if (hasDeletedLayers)
            {
                ActiveSelectionContext = TimelineSelectionContext.None;
                ReindexTrackNames();
                FinalizeDeleteChanges();
            }
        }

        public void RefreshLayerSelectionVisuals()
        {
            foreach (var track in Tracks)
            {
                track.HasSelectedChildren = TrackHasSelectedChildren(track);
            }
        }

        private bool TrackHasSelectedChildren(TrackViewModel track)
        {
            if (track.UIOffsetKeyframes.Any(k => k.IsSelected) ||
                track.UIScaleKeyframes.Any(k => k.IsSelected) ||
                track.UIRotationKeyframes.Any(k => k.IsSelected) ||
                track.UIOpacityKeyframes.Any(k => k.IsSelected) ||
                track.UISpeedKeyframes.Any(k => k.IsSelected))
            {
                return true;
            }

            foreach (var note in track.UINotes)
            {
                if (note.IsSelected ||
                    note.UIOffsetKeyframes.Any(k => k.IsSelected) ||
                    note.UIScaleKeyframes.Any(k => k.IsSelected) ||
                    note.UIRotationKeyframes.Any(k => k.IsSelected) ||
                    note.UIOpacityKeyframes.Any(k => k.IsSelected) ||
                    note.UINoteKindKeyframes.Any(k => k.IsSelected))
                {
                    return true;
                }
            }

            return false;
        }

        private void FinalizeDeleteChanges()
        {
            RefreshLayerSelectionVisuals();
            NotifyKeyframeClipboardCommandsStateChanged();

            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            AudioTrack?.UpdatePixels();

            int currentTick = GetCurrentTick();
            var easingDirection = CurrentChart.KeyFrameEasingDirection;
            BpmTrack?.SyncValuesToTime(currentTick);
            foreach (var track in Tracks)
            {
                track.SyncValuesToTime(currentTick, easingDirection);
            }
        }

        private void ReindexTrackNames()
        {
            for (int i = 0; i < Tracks.Count; i++)
            {
                Tracks[i].TrackName = $"判定线图层 {i + 1}";
            }
        }



        // TimelineViewModel.cs 里新增：

        /// <summary>
        /// 智能磁吸算法：根据当前缩放比例，寻找最近的小节线或关键帧
        /// </summary>
        // 🌟 新增参数 isPlayhead，用来告诉雷达：“现在是不是游标正在移动？”
        public int SnapToClosest(double exactTickDouble, bool isPlayhead = false)
        {
            // 没按 Shift，原样返回，完全不吸附
            if (!System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                return (int)Math.Round(exactTickDouble, MidpointRounding.AwayFromZero);

            int rawTick = (int)Math.Round(exactTickDouble, MidpointRounding.AwayFromZero);
            double pixelsPerTick = TickToPixel(1000) / 1000.0;
            if (pixelsPerTick <= 0) return rawTick;

            int snapThresholdPixels = 12;
            int tickThreshold = (int)(snapThresholdPixels / pixelsPerTick);

            int bestTick = rawTick;
            double minDiff = double.MaxValue;

            // ================= A. 动态网格线吸附 =================
            int[] intervals = { 128, 64, 32, 16, 8, 4, 2 };
            int currentInterval = 128;

            foreach (var interval in intervals)
            {
                if (interval * pixelsPerTick >= 20)
                    currentInterval = interval;
                else
                    break;
            }

            int gridTick = (int)Math.Round((double)rawTick / currentInterval) * currentInterval;
            if (Math.Abs(gridTick - rawTick) <= tickThreshold)
            {
                bestTick = gridTick;
                minDiff = Math.Abs(gridTick - rawTick);
            }

            // ================= B. 全局元素吸附 =================
            void TrySnap(int targetTick)
            {
                int diff = Math.Abs(targetTick - rawTick);
                if (diff <= tickThreshold && diff < minDiff)
                {
                    minDiff = diff;
                    bestTick = targetTick;
                }
            }

            // 🌟 绝佳体验：如果拖拽的不是游标，让物体也能吸附到静止的游标上！
            if (!isPlayhead)
            {
                int playheadTick = (int)Math.Round(PixelToTick(PlayheadPositionX));
                TrySnap(playheadTick);
            }

            // 🌟 权限判定：游标拖拽时可以吸附【被选中】的元素，但物体拖拽时必须无视【选中的自己】
            bool ShouldIgnore(bool isSelected) => !isPlayhead && isSelected;

            // 1. 扫荡全局 BPM
            if (BpmTrack != null)
                foreach (var kf in BpmTrack.UIBpmKeyframes)
                    if (!ShouldIgnore(kf.IsSelected)) TrySnap(kf.Model.Time);

            // 2. 扫荡所有轨道
            foreach (var track in Tracks)
            {
                // 判定线自身的关键帧
                foreach (var kf in track.UIOffsetKeyframes) if (!ShouldIgnore(kf.IsSelected)) TrySnap(kf.Model.Time);
                foreach (var kf in track.UIScaleKeyframes) if (!ShouldIgnore(kf.IsSelected)) TrySnap(kf.Model.Time);
                foreach (var kf in track.UIRotationKeyframes) if (!ShouldIgnore(kf.IsSelected)) TrySnap(kf.Model.Time);
                foreach (var kf in track.UIOpacityKeyframes) if (!ShouldIgnore(kf.IsSelected)) TrySnap(kf.Model.Time);
                foreach (var kf in track.UISpeedKeyframes) if (!ShouldIgnore(kf.IsSelected)) TrySnap(kf.Model.Time);

                // 音符本体及其尾巴
                foreach (var note in track.UINotes)
                {
                    if (!ShouldIgnore(note.IsSelected))
                    {
                        TrySnap(note.Model.HitTime);
                        if (note.CurrentNoteKind == Axphi.Data.NoteKind.Hold)
                            TrySnap(note.Model.HitTime + note.HoldDuration);
                    }

                    // 🌟 重点补漏：把音符【内部】的所有关键帧也扔进雷达！
                    foreach (var kf in note.UIOffsetKeyframes) if (!ShouldIgnore(kf.IsSelected)) TrySnap(kf.Model.Time);
                    foreach (var kf in note.UIScaleKeyframes) if (!ShouldIgnore(kf.IsSelected)) TrySnap(kf.Model.Time);
                    foreach (var kf in note.UIRotationKeyframes) if (!ShouldIgnore(kf.IsSelected)) TrySnap(kf.Model.Time);
                    foreach (var kf in note.UIOpacityKeyframes) if (!ShouldIgnore(kf.IsSelected)) TrySnap(kf.Model.Time);

                    if (note.UINoteKindKeyframes != null)
                        foreach (var kf in note.UINoteKindKeyframes) if (!ShouldIgnore(kf.IsSelected)) TrySnap(kf.Model.Time);
                }
            }

            return bestTick;
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
        partial void OnWorkspaceStartTickChanged(int value) => UpdateWorkspacePixels();
        partial void OnWorkspaceEndTickChanged(int value) => UpdateWorkspacePixels();

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

            // 1. 算出整首曲子“绝对尽头 (TotalDurationTicks)”的物理秒数
            double totalSeconds = Axphi.Utilities.TimeTickConverter.TickToTime(
                TotalDurationTicks, CurrentChart.BpmKeyFrames, CurrentChart.InitialBpm);

            // 2. 算出工作区右边界的物理秒数
            double workspaceEndSeconds = Axphi.Utilities.TimeTickConverter.TickToTime(
                WorkspaceEndTick, CurrentChart.BpmKeyFrames, CurrentChart.InitialBpm);

            bool shouldLoop = false;

            // 核心拦截 A：正常越过工作区 (从工作区内部穿过右侧手柄)
            if (WorkspaceStartTick < WorkspaceEndTick &&
                prevTimeSeconds < workspaceEndSeconds &&
                currentTimeSeconds >= workspaceEndSeconds)
            {
                shouldLoop = true;
            }
            // 核心拦截 B：游标已经在工作区外，并且一路播放到了整首歌的尽头！
            else if (currentTimeSeconds >= totalSeconds)
            {
                shouldLoop = true;
            }

            // 执行跳回动作
            if (shouldLoop)
            {
                // 算出工作区左边界的物理秒数
                double startSeconds = Axphi.Utilities.TimeTickConverter.TickToTime(
                    WorkspaceStartTick, CurrentChart.BpmKeyFrames, CurrentChart.InitialBpm);

                // 强行将大管家的时间拽回工作区起跑线
                CurrentPlayTimeSeconds = startSeconds;

                // 发送加急信：命令渲染器和底层音频引擎立刻空降回这个时间重播！
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ForceSeekMessage(startSeconds));

                // 强制画面同步刷新
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
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