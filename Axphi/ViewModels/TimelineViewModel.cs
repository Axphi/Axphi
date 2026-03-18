using Axphi.Data; // 替换成你 Chart 和 JudgementLine 所在的实际命名空间
using Axphi.Data.KeyFrames;
using Axphi.Services;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq;

namespace Axphi.ViewModels
{
    // 必须继承 ObservableObject 才能使用 MVVM 魔法
    public partial class TimelineViewModel : ObservableObject
    {

        // 【新增】保存全局数据源的引用
        private readonly ProjectManager _projectManager;

        // 核心数据：需要暴露给界面的谱面对象
        [ObservableProperty]
        private Chart _currentChart = new Chart();

        // 1. 缩放比例 (Zoom)：相当于按住 Alt 滚轮修改的值，默认是 1.0
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPixelWidth))] // 当 Zoom 改变时，通知界面重新获取总宽度
        private double _zoomScale = 1.0;

        // 2. 谱面总长度 (以 128分音符/Tick 为单位)
        // 假设一首 2 分钟的 120BPM 歌曲，大约有 240 拍 * 32 = 7680 个 Tick。我们先给个默认值 10000 够长了。
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPixelWidth))]
        private int _totalDurationTicks = 10000;

        // 3. 基础缩放系数：1 个 Tick 默认占多少像素？
        // 如果给 1.0，那 10000 个 Tick 就是 10000 像素，太长了。我们默认给个 0.5 像素试试。
        private const double BasePixelsPerTick = 0.5;

        // 4. 【核心魔法】计算出右侧轨道的物理总像素宽度！UI 会绑定这个值！
        public double TotalPixelWidth => TotalDurationTicks * BasePixelsPerTick * ZoomScale;

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

        // ================= 新增：专供 UI 绑定的轨道视图模型集合 =================
        public ObservableCollection<TrackViewModel> Tracks { get; } = new ObservableCollection<TrackViewModel>();
        


        // 构造函数：初始化时，可以先给个空谱面，或者由外部传进来
        public TimelineViewModel(ProjectManager projectManager)
        {

            _projectManager = projectManager; // 存进私有变量


            // 🌟 必须加上这行！让它一出生就计算宽度！
            UpdateWorkspacePixels();


            // 然后再从 manager 里把 Chart 拿出来赋值给 _currentChart
            // 极其重要的防坑提示：软件刚启动时，工程可能是空的！所以要做个判空！
            if (_projectManager.EditingProject != null)
            {
                ReloadTracksFromCurrentChart();
            }


            WeakReferenceMessenger.Default.Register<TimelineViewModel, ProjectLoadedMessage>(this, (recipient, message) =>
            {
                // 重新去抱 ProjectManager 的大腿！拿到最新的“谱面B”！
                if (recipient._projectManager.EditingProject != null)
                {
                    recipient.CurrentChart = recipient._projectManager.EditingProject.Chart;
                    recipient.Tracks.Clear();
                    // 收到换工程的广播后，立刻执行重建动作！
                    recipient.ReloadTracksFromCurrentChart();
                }
            });


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

        // ================= 【新增的核心函数】 =================
        /// <summary>
        /// 根据当前 ProjectManager 里的真实谱面，重新生成左侧的所有 Track UI
        /// </summary>
        private void ReloadTracksFromCurrentChart()
        {
            if (_projectManager.EditingProject == null || _projectManager.EditingProject.Chart == null)
                return;

            // 1. 换绑剧本：把指针指向最新的真实谱面
            CurrentChart = _projectManager.EditingProject.Chart;

            // ================= ✨ 塞入第一步：实例化 BPM 轨道！ =================
            BpmTrack = new BpmTrackViewModel(CurrentChart, this);
            // =====================================================================

            // 2. 砸碎旧舞台：清空前端的 Track UI 集合 (这一步让旧 UI 被 GC 回收)
            Tracks.Clear();

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

            // 4. (可选) 让时间轴游标归零
            CurrentPlayTimeSeconds = 0;

            // 🌟 【新增核心修复】：同时发信给右侧渲染器和音频播放器，强制它们也空降回 0 秒！
            // 这样前后端的记忆就彻底统一了！
            WeakReferenceMessenger.Default.Send(new ForceSeekMessage(0));

            UpdateWorkspacePixels();

            // 5. 顺便大喊一声，让右侧的渲染器也强制刷新一下画面！
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());

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
            return exactTick + CurrentChart.Offset;
        }

        // ================= 核心命令：全局删除选中的关键帧 =================
        [RelayCommand]
        private void DeleteSelectedKeyframes()
        {
            
            bool hasDeleted = false;

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
                    hasDeleted = true;
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
                    hasDeleted = true;
                }
                // Scale
                var scaleToDelete = track.UIScaleKeyframes.Where(k => k.IsSelected).ToList();
                foreach (var wrapper in scaleToDelete)
                {
                    track.Data.AnimatableProperties.Scale.KeyFrames.Remove((ScaleKeyFrame)wrapper.Model);
                    track.UIScaleKeyframes.Remove(wrapper);
                    hasDeleted = true;
                }
                // Rotation
                var rotationToDelete = track.UIRotationKeyframes.Where(k => k.IsSelected).ToList();
                foreach (var wrapper in rotationToDelete)
                {
                    track.Data.AnimatableProperties.Rotation.KeyFrames.Remove((RotationKeyFrame)wrapper.Model);
                    track.UIRotationKeyframes.Remove(wrapper);
                    hasDeleted = true;
                }
                // Opacity
                var opacityToDelete = track.UIOpacityKeyframes.Where(k => k.IsSelected).ToList();
                foreach (var wrapper in opacityToDelete)
                {
                    track.Data.AnimatableProperties.Opacity.KeyFrames.Remove((OpacityKeyFrame)wrapper.Model);
                    track.UIOpacityKeyframes.Remove(wrapper);
                    hasDeleted = true;
                }

                // ================= B. 删音符自己的关键帧 =================
                foreach (var note in track.UINotes)
                {
                    // Note Offset
                    var noteOffsetDel = note.UIOffsetKeyframes.Where(k => k.IsSelected).ToList();
                    foreach (var wrapper in noteOffsetDel) { note.Model.AnimatableProperties.Offset.KeyFrames.Remove((OffsetKeyFrame)wrapper.Model); note.UIOffsetKeyframes.Remove(wrapper); hasDeleted = true; }

                    // Note Scale
                    var noteScaleDel = note.UIScaleKeyframes.Where(k => k.IsSelected).ToList();
                    foreach (var wrapper in noteScaleDel) { note.Model.AnimatableProperties.Scale.KeyFrames.Remove((ScaleKeyFrame)wrapper.Model); note.UIScaleKeyframes.Remove(wrapper); hasDeleted = true; }

                    // Note Rotation
                    var noteRotDel = note.UIRotationKeyframes.Where(k => k.IsSelected).ToList();
                    foreach (var wrapper in noteRotDel) { note.Model.AnimatableProperties.Rotation.KeyFrames.Remove((RotationKeyFrame)wrapper.Model); note.UIRotationKeyframes.Remove(wrapper); hasDeleted = true; }

                    // Note Opacity
                    var noteOpaDel = note.UIOpacityKeyframes.Where(k => k.IsSelected).ToList();
                    foreach (var wrapper in noteOpaDel) { note.Model.AnimatableProperties.Opacity.KeyFrames.Remove((OpacityKeyFrame)wrapper.Model); note.UIOpacityKeyframes.Remove(wrapper); hasDeleted = true; }

                    // ✨ 新增：Note Kind 关键帧删除
                    if (note.Model.KindKeyFrames != null)
                    {
                        var noteKindDel = note.UINoteKindKeyframes.Where(k => k.IsSelected).ToList();
                        foreach (var wrapper in noteKindDel)
                        {
                            note.Model.KindKeyFrames.Remove((NoteKindKeyFrame)wrapper.Model);
                            note.UINoteKindKeyframes.Remove(wrapper);
                            hasDeleted = true;
                        }
                    }
                }

                

                // ================= C. 直接删掉被选中的音符本体！ =================
                var notesToDelete = track.UINotes.Where(n => n.IsSelected).ToList();
                foreach (var note in notesToDelete)
                {
                    track.Data.Notes.Remove(note.Model);
                    track.UINotes.Remove(note);
                    hasDeleted = true;

                    // 安全防护：如果删掉的正好是正在属性面板显示的那个音符，把面板清空
                    if (track.SelectedNote == note)
                    {
                        track.SelectedNote = null;
                    }
                }
            }




            // 3. 善后工作：如果真的删了东西，通知渲染器和左侧面板更新！
            if (hasDeleted)
            {
                // 发信重绘右侧画面
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());

                // 强制刷新一次左侧面板的数值，防止删掉关键帧后数值还停留在幽灵状态
                int currentTick = GetCurrentTick();
                var easingDirection = CurrentChart.KeyFrameEasingDirection;
                BpmTrack?.SyncValuesToTime(currentTick);
                foreach (var track in Tracks)
                {
                    track.SyncValuesToTime(currentTick, easingDirection);
                }
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
        }
        // ================= 工作区循环拦截引擎 =================
        // 供外部播放引擎在“正在播放”状态下每一帧调用
        // ================= 工作区循环拦截引擎 =================
        // 🌟 增加两个参数：接收上一帧的时间和当前帧的时间
        public void CheckWorkspaceLoop(double prevTimeSeconds, double currentTimeSeconds)
        {
            if (CurrentChart == null) return;

            // 1. 安全锁：防止左右手柄捏在一起重合，导致死循环爆炸
            if (WorkspaceStartTick >= WorkspaceEndTick) return;

            // 2. 算出工作区右边界的物理秒数
            double endSeconds = Axphi.Utilities.TimeTickConverter.TickToTime(WorkspaceEndTick, CurrentChart.BpmKeyFrames, CurrentChart.InitialBpm);

            // 3. 核心拦截：🌟 只有当“上一瞬还在界内（或左侧），且这一瞬刚好越界”时，才触发循环！
            if (prevTimeSeconds < endSeconds && currentTimeSeconds >= endSeconds)
            {
                // 算出左边界的物理秒数
                double startSeconds = Axphi.Utilities.TimeTickConverter.TickToTime(WorkspaceStartTick, CurrentChart.BpmKeyFrames, CurrentChart.InitialBpm);

                // 强行将大管家的时间拽回左边界
                CurrentPlayTimeSeconds = startSeconds;

                // 寄加急信：命令右侧的渲染器和底层的音频播放器，立刻给我空降回这个时间重播！
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ForceSeekMessage(startSeconds));

                // 确保画面同步刷新
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
        }
    }



  
}