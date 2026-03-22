using Axphi.Data;
using Axphi.Data.KeyFrames; // 引入你的 OffsetKeyFrame
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // 用来发重绘消息
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using Axphi.Utilities; // 引入 EasingUtils 所在的命名空间

namespace Axphi.ViewModels
{
    public partial class TrackViewModel : ObservableObject
    {

        // 1. 保留原来的字段，这样你下面的代码（比如 _timeline.GetCurrentTick()）都不用改！
        public TimelineViewModel _timeline;

        // 🌟 2. 新增一个公开属性，专门给 XAML 界面绑定用！
        public TimelineViewModel Timeline => _timeline;

        // 【新增】免死金牌标志位
        private bool _isSyncing = false;

        // ================= 专供 UI 绑定的小菱形替身集合 =================
        // 把原来直接装底层 KeyFrame 的集合，换成装保镖的集合
        // ================= 1. 声明四个 UI 替身集合 =================
        // Offset 和 Scale 是 X,Y 两个值，所以是 Vector
        public ObservableCollection<KeyFrameUIWrapper<Vector>> UIOffsetKeyframes { get; } = new();
        public ObservableCollection<KeyFrameUIWrapper<Vector>> UIScaleKeyframes { get; } = new();
        // Rotation 和 Opacity 只有一个值，所以是 double
        public ObservableCollection<KeyFrameUIWrapper<double>> UIRotationKeyframes { get; } = new();
        public ObservableCollection<KeyFrameUIWrapper<double>> UIOpacityKeyframes { get; } = new();


        // 供 UI 绑定的替身集合
        public ObservableCollection<KeyFrameUIWrapper<double>> UISpeedKeyframes { get; } = new();

        // 供 XAML 左侧属性面板绑定的当前速度
        [ObservableProperty]
        private double _currentSpeed = 1.0; // 默认值和底层 InitialSpeed 保持一致
        [ObservableProperty]
        private string _currentSpeedMode = "Integral";

        // ================= 1. 底层数据源 =================
        // 这个属性是只读的，它紧紧抓住那个不会被污染的底层老实人
        public JudgementLine Data { get; }

        // ================= 2. 纯 UI 状态（不参与 JSON 导出） =================
        [ObservableProperty]
        private bool _isExpanded; // 记录左侧的属性面板是否展开（v 和 >）

        [ObservableProperty]
        private string _trackName; // 轨道的名字，比如 "判定线 1"

        // ================= 3. 供 DraggableValueBox 绑定的双向数据 =================
        [ObservableProperty]
        private double _currentOffsetX;

        [ObservableProperty]
        private double _currentOffsetY;

        [ObservableProperty]
        private double _currentScaleX = 1.0; // 默认缩放给 1

        [ObservableProperty]
        private double _currentScaleY = 1.0;

        [ObservableProperty]
        private double _currentRotation;

        [ObservableProperty]
        private double _currentOpacity = 100.0; // 默认透明度给 100

        

        // ================= 4. 构造函数 =================
        public TrackViewModel(JudgementLine data, string name,TimelineViewModel timeline)
        {
            Data = data;
            TrackName = name;
            _timeline = timeline;

            // 🌟 1. 出生时，读取底层的寿命数据
            LayerStartTick = Data.StartTick;
            LayerDurationTicks = Data.DurationTicks;


            // 如果底层数据里已经有关键帧了，把它们请进 UI 替身集合里
            // 初始化时，把底层已有的关键帧全部包上一层保镖！
            // ================= 2. 构造时，把底层已有的数据全部包上保镖 =================
            if (Data.AnimatableProperties.Offset.KeyFrames != null)
                foreach (var kf in Data.AnimatableProperties.Offset.KeyFrames)
                    UIOffsetKeyframes.Add(new KeyFrameUIWrapper<Vector>(kf, _timeline));

            if (Data.AnimatableProperties.Scale.KeyFrames != null)
                foreach (var kf in Data.AnimatableProperties.Scale.KeyFrames)
                    UIScaleKeyframes.Add(new KeyFrameUIWrapper<Vector>(kf, _timeline));

            if (Data.AnimatableProperties.Rotation.KeyFrames != null)
                foreach (var kf in Data.AnimatableProperties.Rotation.KeyFrames)
                    UIRotationKeyframes.Add(new KeyFrameUIWrapper<double>(kf, _timeline));

            if (Data.AnimatableProperties.Opacity.KeyFrames != null)
                foreach (var kf in Data.AnimatableProperties.Opacity.KeyFrames)
                    UIOpacityKeyframes.Add(new KeyFrameUIWrapper<double>(kf, _timeline));

            // 【加在构造函数里：把底层的 Speed 关键帧包上保镖】
            if (Data.SpeedKeyFrames != null)
                foreach (var kf in Data.SpeedKeyFrames)
                    UISpeedKeyframes.Add(new KeyFrameUIWrapper<double>(kf, _timeline));



            // 构造时，把底层判定线里带的音符全部转化为 NoteViewModel 塞进集合
            if (Data.Notes != null)
            {
                foreach (var note in Data.Notes)
                {
                    UINotes.Add(new NoteViewModel(note, _timeline));
                }
            }

            // ================= ✨ 修复初始值不更新的 Bug =================
            // 刚出生时，立刻强行同步一次当前时间的数据！这样一显示就是对的！
            int currentTick = _timeline.GetCurrentTick();
            var easingDir = _timeline.CurrentChart.KeyFrameEasingDirection; // 从大管家那拿全局缓动方向


            CurrentSpeedMode = Data.SpeedMode ?? "Integral";


            // 1. 同步轨道自己的四个属性
            this.SyncValuesToTime(currentTick, easingDir);

            // 2. 同步底下所有音符的四个属性
            foreach (var note in UINotes)
            {
                note.SyncValuesToTime(currentTick, easingDir);
            }


            // 放在构造函数里初始化一下
            LayerPixelWidth = _timeline.TickToPixel(LayerDurationTicks);
            // 当收到缩放比例改变的信件时，立刻根据绝对时间重算像素偏移！
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<TrackViewModel, ZoomScaleChangedMessage>(this, (r, m) =>
            {
                r.LayerPixelXOffset = r._timeline.TickToPixel(r.LayerStartTick);
                // 🌟 新增：缩放时宽度也要跟着变！
                r.LayerPixelWidth = r._timeline.TickToPixel(r.LayerDurationTicks);
            });


            WeakReferenceMessenger.Default.Register<TrackViewModel, KeyframesNeedSortMessage>(this, (r, m) =>
            {
                // 只要松手，就把四个轨道的底层 List 强制按时间重排，并把撞车的关键帧直接吞噬！
                r.SortAndMergeDuplicates(r.Data.AnimatableProperties.Offset.KeyFrames, r.UIOffsetKeyframes);
                r.SortAndMergeDuplicates(r.Data.AnimatableProperties.Scale.KeyFrames, r.UIScaleKeyframes);
                r.SortAndMergeDuplicates(r.Data.AnimatableProperties.Rotation.KeyFrames, r.UIRotationKeyframes);
                r.SortAndMergeDuplicates(r.Data.AnimatableProperties.Opacity.KeyFrames, r.UIOpacityKeyframes);
                // 2. ✨ 新增：遍历底层所有的音符，给它们自己的关键帧也排个序！
                foreach (var note in r.UINotes)
                {
                    r.SortAndMergeDuplicates(note.Model.AnimatableProperties.Offset.KeyFrames, note.UIOffsetKeyframes);
                    r.SortAndMergeDuplicates(note.Model.AnimatableProperties.Scale.KeyFrames, note.UIScaleKeyframes);
                    r.SortAndMergeDuplicates(note.Model.AnimatableProperties.Rotation.KeyFrames, note.UIRotationKeyframes);
                    r.SortAndMergeDuplicates(note.Model.AnimatableProperties.Opacity.KeyFrames, note.UIOpacityKeyframes);

                    // 专门给 NoteKind 的关键帧排序！
                    if (note.Model.KindKeyFrames != null)
                    {
                        r.SortAndMergeDuplicates(note.Model.KindKeyFrames, note.UINoteKindKeyframes);
                    }


                }



                


                // 【加在 1号邮局 KeyframesNeedSortMessage 的里面：给 Speed 排序去重】
                r.SortAndMergeDuplicates(r.Data.SpeedKeyFrames, r.UISpeedKeyframes);



                // 🌟 新增：排完序后，立刻重新计算大家的车道和重叠！
                r.RecalculateLanes();



                // 杀完人后，通知右侧渲染器和左侧面板重新加载一次画面，防残留！
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());

                
            });

            // 🌟 新增：专门接收“音符需要重排和算碰撞”的专属邮局！
            WeakReferenceMessenger.Default.Register<TrackViewModel, NotesNeedSortMessage>(this, (r, m) =>
            {
                // 收到信件后，立刻重新计算大家的碰撞和车道！
                r.RecalculateLanes();

                // 通知右侧重绘
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            });

            WeakReferenceMessenger.Default.Register<TrackViewModel, ClearSelectionMessage>(this, (r, m) =>
            {
                if (m.GroupName == "Layers" && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.IsLayerSelected = false;
                }
            });

            WeakReferenceMessenger.Default.Register<TrackViewModel, LayersDragStartedMessage>(this, (r, m) =>
            {
                if (r.IsLayerSelected && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.ReceiveLayerDragStarted();
                }
            });

            WeakReferenceMessenger.Default.Register<TrackViewModel, LayersDragDeltaMessage>(this, (r, m) =>
            {
                if (r.IsLayerSelected && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.ReceiveLayerDragDelta(m.HorizontalChange, m.DeltaTick);
                }
            });

            WeakReferenceMessenger.Default.Register<TrackViewModel, LayersDragCompletedMessage>(this, (r, m) =>
            {
                if (r.IsLayerSelected && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.ReceiveLayerDragCompleted();
                }
            });

            RecalculateLanes();
        }

        // ================= 5. 核心拦截器 (黑魔法) =================
        // 当你在界面上按住 DraggableValueBox 左右拖拽时，这个方法会被疯狂触发！
        // ================= 5. 核心拦截器：双向绑定的终极魔法 =================
        // ----- Position (Offset) -----
        partial void OnCurrentOffsetXChanged(double value)
        {
            // 如果是播放器在推着数值走，千万别动！
            if (_isSyncing) return;
            // 核心：不管三七二十一，只要人类动手了，立刻大喊一声“刹车！”
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            // 如果是人手在拖拽：直接调用我们写好的“添加/修改关键帧”逻辑！
            // 智能判断：如果完全没有关键帧，只修改基础值
            if (Data.AnimatableProperties.Offset.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Offset.InitialValue = new Vector(CurrentOffsetX, CurrentOffsetY);
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddPositionKeyframe();
            }
        }

        partial void OnCurrentOffsetYChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            if (Data.AnimatableProperties.Offset.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Offset.InitialValue = new Vector(CurrentOffsetX, CurrentOffsetY);
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddPositionKeyframe();
            }
        }

        // ----- Scale -----
        partial void OnCurrentScaleXChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            if (Data.AnimatableProperties.Scale.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Scale.InitialValue = new Vector(CurrentScaleX, CurrentScaleY);
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddScaleKeyframe();
            }
        }

        partial void OnCurrentScaleYChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            if (Data.AnimatableProperties.Scale.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Scale.InitialValue = new Vector(CurrentScaleX, CurrentScaleY);
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddScaleKeyframe();
            }
        }

        // ----- Rotation -----
        partial void OnCurrentRotationChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            if (Data.AnimatableProperties.Rotation.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Rotation.InitialValue = CurrentRotation;
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddRotationKeyframe();
            }
        }

        // ----- Opacity -----
        partial void OnCurrentOpacityChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            if (Data.AnimatableProperties.Opacity.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Opacity.InitialValue = CurrentOpacity;
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddOpacityKeyframe();
            }
        }

        partial void OnCurrentSpeedModeChanged(string value)
        {
            if (_isSyncing || Data == null) return;

            Data.SpeedMode = value; // 同步给底层
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage()); // 瞬间重绘！
        }

        // ================= 添加 Offset (Position) 关键帧 =================
        [RelayCommand]
        private void AddPositionKeyframe()
        {
            // 1. 问爸爸：现在是第几个 Tick？
            int currentTick = _timeline.GetCurrentTick();

            // 2. 顺藤摸瓜，拿到你底层数据里的 Offset KeyFrames 集合
            var offsetKeyframesData = Data.AnimatableProperties.Offset.KeyFrames; // 底层的纯净 List

            // 重点：我们从保镖集合里去找有没有当前时间的
            var existingWrapper = UIOffsetKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            

            if (existingWrapper != null)
            {
                // 如果有，直接修改它的值
                // 1. 修改底层 
                // 如果有了，直接修改保镖手里的那个底层 Model！
                existingWrapper.Model.Value = new System.Windows.Vector(CurrentOffsetX, CurrentOffsetY);
                
            }
            else
            {
                // 如果没有，New 一个底层的，再立刻给它配个保镖！
                var newFrame = new OffsetKeyFrame()
                {
                    Time = currentTick,
                    Value = new System.Windows.Vector(CurrentOffsetX, CurrentOffsetY)
                };

                offsetKeyframesData.Add(newFrame); // 存入底层

                // 【核心修复】：每次添加完新的，立刻让底层 List 按时间 (Time) 重新排队！
                offsetKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));

                UIOffsetKeyframes.Add(new KeyFrameUIWrapper<Vector>(newFrame, _timeline)); // 生成 UI 显示
            }

            // 4. 发广播通知右侧的 ChartRenderer 重新画一下谱面
            // (借用一下你之前写的 JudgementLinesChangedMessage)
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        // ================= Scale 命令 =================
        [RelayCommand]
        private void AddScaleKeyframe()
        {
            int currentTick = _timeline.GetCurrentTick();
            var scaleKeyframesData = Data.AnimatableProperties.Scale.KeyFrames;
            var existingWrapper = UIScaleKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = new Vector(CurrentScaleX, CurrentScaleY);
            }
            else
            {
                // 假设你有一个 ScaleKeyFrame 继承自 KeyFrame<Vector>
                var newFrame = new ScaleKeyFrame() { Time = currentTick, Value = new Vector(CurrentScaleX, CurrentScaleY) };
                scaleKeyframesData.Add(newFrame);
                scaleKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UIScaleKeyframes.Add(new KeyFrameUIWrapper<Vector>(newFrame, _timeline));
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        // ================= Rotation 命令 =================
        [RelayCommand]
        private void AddRotationKeyframe()
        {
            int currentTick = _timeline.GetCurrentTick();
            var rotationKeyframesData = Data.AnimatableProperties.Rotation.KeyFrames;
            var existingWrapper = UIRotationKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = CurrentRotation;
            }
            else
            {
                // 假设你有 RotationKeyFrame 继承自 KeyFrame<double>
                var newFrame = new RotationKeyFrame() { Time = currentTick, Value = CurrentRotation };
                rotationKeyframesData.Add(newFrame);
                rotationKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UIRotationKeyframes.Add(new KeyFrameUIWrapper<double>(newFrame, _timeline));
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        // ================= Opacity 命令 =================
        [RelayCommand]
        private void AddOpacityKeyframe()
        {
            int currentTick = _timeline.GetCurrentTick();
            var opacityKeyframesData = Data.AnimatableProperties.Opacity.KeyFrames;
            var existingWrapper = UIOpacityKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = CurrentOpacity;
            }
            else
            {
                // 假设你有 OpacityKeyFrame 继承自 KeyFrame<double>
                var newFrame = new OpacityKeyFrame() { Time = currentTick, Value = CurrentOpacity };
                opacityKeyframesData.Add(newFrame);
                opacityKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UIOpacityKeyframes.Add(new KeyFrameUIWrapper<double>(newFrame, _timeline));
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }


        // ================= Speed 拦截器 =================
        partial void OnCurrentSpeedChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());

            // 注意：这里直接访问 Data.SpeedKeyFrames
            if (Data.SpeedKeyFrames.Count == 0)
            {
                Data.InitialSpeed = CurrentSpeed;
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddSpeedKeyframe();
            }
        }

        // ================= Speed 添加命令 =================
        [RelayCommand]
        private void AddSpeedKeyframe()
        {
            int currentTick = _timeline.GetCurrentTick();
            var speedKeyframesData = Data.SpeedKeyFrames; // 直接拿底层的 List
            var existingWrapper = UISpeedKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = CurrentSpeed;
            }
            else
            {
                // 直接 new 一个通用的 KeyFrame<double>
                var newFrame = new KeyFrame<double>() { Time = currentTick, Value = CurrentSpeed };
                speedKeyframesData.Add(newFrame);
                speedKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UISpeedKeyframes.Add(new KeyFrameUIWrapper<double>(newFrame, _timeline));
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        // 【新增】暴露给大管家调用的同步方法
        public void SyncValuesToTime(int currentTick, KeyFrameEasingDirection direction)
        {
            _isSyncing = true; // 举起免死金牌：现在是机器在更新数据，不是人在拖拽！

            // 召唤你底层写好的算力神兽！直接白嫖它的插值结果！
            EasingUtils.CalculateObjectTransform(
                currentTick,
                direction,
                Data.AnimatableProperties,
                out var offset, out var scale, out var rotationAngle, out var opacity);

            // 把算出来的精确数值，直接塞给前端 UI 绑定的属性！
            CurrentOffsetX = offset.X;
            CurrentOffsetY = offset.Y;
            CurrentScaleX = scale.X;
            CurrentScaleY = scale.Y;
            CurrentRotation = rotationAngle;
            CurrentOpacity = opacity;

            // ================= 🌟 新增：单独呼叫泛型神兽算出 Speed！ =================
            // 因为 Speed 也是 double 类型，插值逻辑和 Opacity 是一模一样的
            EasingUtils.CalculateObjectSingleTransform(
                currentTick,
                direction,
                Data.InitialSpeed,       // 基础值
                Data.SpeedKeyFrames,     // 关键帧集合
                Axphi.Utilities.MathUtils.Lerp, // 你的双精度线性插值函数
                out var finalSpeed);     // 吐出结果

            CurrentSpeed = finalSpeed;   // 塞给前端 UI！
            // =======================================================================



            _isSyncing = false; // 放下免死金牌
        }


        // 🌟 核心修复：引入了 TKeyFrame 泛型约束
        private void SortAndMergeDuplicates<T, TKeyFrame>(List<TKeyFrame> dataList, ObservableCollection<KeyFrameUIWrapper<T>> uiList)
            where T : struct
            where TKeyFrame : KeyFrame<T> // 告诉编译器，TKeyFrame 肯定是 KeyFrame<T> 的子类
        {
            // 1. 先按时间排好队
            dataList.Sort((a, b) => a.Time.CompareTo(b.Time));

            // 2. 倒序遍历检查碰撞（倒序遍历时删除元素才不会导致索引越界报错）
            for (int i = dataList.Count - 1; i > 0; i--)
            {
                // 发现两辆车撞在同一时间点了！
                if (dataList[i].Time == dataList[i - 1].Time)
                {
                    var modelA = dataList[i - 1];
                    var modelB = dataList[i];

                    var wrapperA = uiList.FirstOrDefault(w => w.Model == modelA);
                    var wrapperB = uiList.FirstOrDefault(w => w.Model == modelB);

                    if (wrapperA != null && wrapperB != null)
                    {
                        // 核心判定规则：谁是被捏在手里拖过来的（被选中），谁就是赢家！
                        KeyFrameUIWrapper<T> victim;

                        if (wrapperA.IsSelected && !wrapperB.IsSelected)
                        {
                            victim = wrapperB; // A是拖过来的，B被覆盖
                        }
                        else if (!wrapperA.IsSelected && wrapperB.IsSelected)
                        {
                            victim = wrapperA; // B是拖过来的，A被覆盖
                        }
                        else
                        {
                            // 万一两个都没选中或者都选中了（极端情况），默认杀掉前面那个
                            victim = wrapperA;
                        }

                        // 无情抹杀：从底层数据和 UI 集合中双重删除
                        // 因为底层 list 要求是确切的子类，所以这里向下转型一下，绝对安全
                        dataList.Remove((TKeyFrame)victim.Model);
                        uiList.Remove(victim);
                    }
                }
            }
        }








        [ObservableProperty]
        private bool _isNoteExpanded; // 记录 note 的属性面板是否展开（v 和 >）

        // ================= 新增：音符管理 =================
        // 专门装 NoteViewModel 的集合！
        public ObservableCollection<NoteViewModel> UINotes { get; } = new();

        // 记录当前选中的是哪个音符，方便 XAML 右侧属性面板绑定！
        [ObservableProperty]
        private NoteViewModel? _selectedNote;

        // ================= 添加新音符 =================
        [RelayCommand]
        private void AddNote(string kindStr)
        {
            // 1. 将 XAML 传进来的字符串 (比如 "Tap") 转化为枚举
            if (!Enum.TryParse<NoteKind>(kindStr, out var kind)) return;

            // 2. 问大管家现在是第几个 Tick
            int currentTick = _timeline.GetCurrentTick();

            // 3. 实例化底层的纯净数据，并加进底层的 List
            var newNote = new Note(kind, currentTick);
            if (Data.Notes == null) Data.Notes = new List<Note>();
            Data.Notes.Add(newNote);

            // 4. 给它套上 UI 保镖装甲！
            var newNoteVM = new NoteViewModel(newNote, _timeline);

            // 🌟 核心防 Bug：出生即同步！强制喂给它当前时间的数据，防止带着默认值出生
            newNoteVM.SyncValuesToTime(currentTick, _timeline.CurrentChart.KeyFrameEasingDirection);

            // 5. 加入 UI 绑定的集合
            UINotes.Add(newNoteVM);

            // ================= 极致的用户体验优化 =================
            // 刚添加完一个音符，立刻让它变成“选中”状态，方便用户接着改它的属性
            WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Notes", null)); // 先清空别人
            newNoteVM.IsSelected = true;
            SelectedNote = newNoteVM; // 左侧面板瞬间切给它！
            IsNoteExpanded = true;    // 确保 Note 的属性面板是展开的

            // 6. 大喊一声：有人空降了！大家重新排个序，顺便刷新一下右侧画面！
            WeakReferenceMessenger.Default.Send(new NotesNeedSortMessage());
        }


        // 供 XAML 绑定，整个轨道动态伸缩的总高度
        [ObservableProperty]
        private double _uITrackHeight = 24; // 默认最少有一条轨道的高度 (24px)

        // ================= 核心：子轨道碰撞检测与自动扩容 (终极防抖版) =================
        public void RecalculateLanes()
        {
            if (UINotes == null || UINotes.Count == 0)
            {
                UITrackHeight = 24;
                return;
            }

            // 🌟 视觉防碰撞缓冲：12像素宽度的隔离带
            int buffer = (int)Math.Round(_timeline.PixelToTick(12));

            // 🌟 核心破局点：先按【当前车道】排，再按【时间】排！
            // 这样在互相穿透时，原先在底下的音符有绝对优先权，杜绝上下乱跳！
            var sortedNotes = UINotes
                .OrderBy(n => n.LaneIndex)
                .ThenBy(n => n.Model.HitTime)
                .ToList();

            // 虚拟车道容器，记录每条车道停了哪些音符
            List<List<NoteViewModel>> lanes = new List<List<NoteViewModel>>();

            // 内部方法：判断某个区间在这条车道里是否空闲
            bool IsLaneFree(int laneIdx, int start, int end)
            {
                if (laneIdx >= lanes.Count) return true;
                foreach (var n in lanes[laneIdx])
                {
                    int nStart = n.Model.HitTime;
                    int nEnd = nStart + (n.CurrentNoteKind == NoteKind.Hold ? n.HoldDuration : 0) + buffer;
                    // 经典的线段重叠检测公式
                    if (Math.Max(start, nStart) < Math.Min(end, nEnd))
                    {
                        return false; // 重叠了
                    }
                }
                return true;
            }

            foreach (var note in sortedNotes)
            {
                int start = note.Model.HitTime;
                int end = start + (note.CurrentNoteKind == NoteKind.Hold ? note.HoldDuration : 0) + buffer;

                int assigned = -1;

                // 永远从最底下的 0 车道开始找空隙
                // 这保证了只要它们分开了，上面的音符会像水一样自动流回最底下！
                for (int i = 0; i < lanes.Count; i++)
                {
                    if (IsLaneFree(i, start, end))
                    {
                        assigned = i;
                        break;
                    }
                }

                // 如果现有车道全被占满了，只能新开辟一条高架桥
                if (assigned == -1)
                {
                    assigned = lanes.Count;
                }

                // 填补容器，防止越界
                while (assigned >= lanes.Count)
                {
                    lanes.Add(new List<NoteViewModel>());
                }

                // 正式入驻新车道
                lanes[assigned].Add(note);
                note.LaneIndex = assigned;
                note.PixelY = assigned * 24;
            }

            // 撑开整个背景的高度
            UITrackHeight = Math.Max(1, lanes.Count) * 24;
        }





        // ================= 图层长度属性 =================
        // 默认给个 7680 个 Tick (比如 60 拍的长度)
        [ObservableProperty]
        private int _layerDurationTicks = 7680;

        [ObservableProperty]
        private double _layerPixelWidth;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLayerHighlighted))]
        private bool _isLayerSelected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLayerHighlighted))]
        private bool _hasSelectedChildren;

        public bool IsLayerHighlighted => IsLayerSelected || HasSelectedChildren;


        [ObservableProperty]
        private double _layerPixelXOffset;

        // 专门用来抵抗 Alt 缩放的绝对物理时间
        [ObservableProperty]
        private int _layerStartTick = 0;

        private double _layerVirtualPixelX;
        private int _lastAppliedTick;
        private bool _wasSelectedBeforeLayerGesture;
        private double _layerGestureDistance;

        public void HandleLayerPointerDown()
        {
            WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Keyframes", null));
            WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Notes", null));
            SelectedNote = null;
            _layerGestureDistance = 0;
            _wasSelectedBeforeLayerGesture = SelectionHelper.BeginSelectionGesture("Layers", this, IsLayerSelected, value => IsLayerSelected = value);
        }

        public void HandleLayerPointerUp()
        {
            SelectionHelper.CompleteSelectionGesture("Layers", this, _wasSelectedBeforeLayerGesture, _layerGestureDistance, value => IsLayerSelected = value);
        }

        public void OnLayerDragStarted()
        {
            if (!IsLayerSelected)
            {
                HandleLayerPointerDown();
            }

            ReceiveLayerDragStarted();

            if (IsLayerSelected)
            {
                WeakReferenceMessenger.Default.Send(new LayersDragStartedMessage(this));
            }
        }

        public void OnLayerDragDelta(double horizontalChange)
        {
            double nextVirtualPixelX = _layerVirtualPixelX + horizontalChange;

            double exactTick = _timeline.PixelToTick(nextVirtualPixelX);
            int snappedTick = _timeline.SnapToClosest(exactTick, isPlayhead: false);
            int deltaTick = snappedTick - _lastAppliedTick;

            if (IsLayerSelected)
            {
                WeakReferenceMessenger.Default.Send(new LayersDragDeltaMessage(horizontalChange, deltaTick, this));
            }

            ReceiveLayerDragDelta(horizontalChange, deltaTick);
        }

        public void OnLayerDragCompleted()
        {
            if (IsLayerSelected)
            {
                WeakReferenceMessenger.Default.Send(new LayersDragCompletedMessage(this));
            }

            ReceiveLayerDragCompleted();
        }

        private void ReceiveLayerDragStarted()
        {
            _layerVirtualPixelX = LayerPixelXOffset;
            _lastAppliedTick = LayerStartTick;
        }

        private void ReceiveLayerDragDelta(double horizontalChange, int deltaTick)
        {
            _layerGestureDistance += Math.Abs(horizontalChange);
            _layerVirtualPixelX += horizontalChange;

            if (deltaTick != 0)
            {
                BatchShiftAllItems(deltaTick);
                _lastAppliedTick += deltaTick;
                Data.StartTick = _lastAppliedTick;
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }

            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
            {
                LayerPixelXOffset = _timeline.TickToPixel(_lastAppliedTick);
            }
            else
            {
                LayerPixelXOffset = _layerVirtualPixelX;
            }
        }

        private void ReceiveLayerDragCompleted()
        {
            LayerStartTick = _lastAppliedTick;
            Data.StartTick = LayerStartTick;
            LayerPixelXOffset = _timeline.TickToPixel(_lastAppliedTick);
        }

        public void BatchShiftAllItems(int deltaTick)
        {
            // 1. 音符大军全体平移
            foreach (var note in UINotes) note.ShiftBy(deltaTick);

            // 2. 判定线自身附带的关键帧平移
            foreach (var kf in UIOffsetKeyframes) kf.ShiftBy(deltaTick);
            foreach (var kf in UIScaleKeyframes) kf.ShiftBy(deltaTick);
            foreach (var kf in UIRotationKeyframes) kf.ShiftBy(deltaTick);
            foreach (var kf in UIOpacityKeyframes) kf.ShiftBy(deltaTick);
            foreach (var kf in UISpeedKeyframes) kf.ShiftBy(deltaTick);

            // 3. 通知全局对关键帧重新排个序，防止因为“防越界”导致部分关键帧堆叠在一起乱了顺序
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new KeyframesNeedSortMessage());
        }
    }


}

