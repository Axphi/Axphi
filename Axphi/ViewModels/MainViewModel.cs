using Axphi.Data;
using Axphi.Services; // 确保引入了 ProjectManager 所在的命名空间
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // Messenger 需要用到这个
using System;
using System.Collections.Generic;
using System.Text;

namespace Axphi.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // 贝塞尔曲线 ViewModel
        public BezierViewModel BezierViewModel { get; }

        // 项目管理器
        public ProjectManager ProjectManager { get; }

        // 文件处理
        
        public FileActionsViewModel FileActions { get; }

        // 注册时间轴子 ViewModel
        public TimelineViewModel Timeline { get; }

        

        // 通过依赖注入，把它们接进来
        public MainViewModel(
            BezierViewModel bezierVM,
            ProjectManager projectManager,
            
            FileActionsViewModel fileActionsViewModel,
            TimelineViewModel timelineViewModel


            )
        {
            BezierViewModel = bezierVM;
            ProjectManager = projectManager;
            
            FileActions = fileActionsViewModel;
            Timeline = timelineViewModel;



        }
        [RelayCommand]
        private void LoadDemoChart()
        {
            ProjectManager.EditingProject = new Project()
            {
                Chart = DebuggingUtils.CreateDemoChart()
            };
            ProjectManager.EditingProjectFilePath = null;
            WeakReferenceMessenger.Default.Send(new ProjectLoadedMessage());
        }
        [RelayCommand]
        private void LoadDemoChart2()
        {
            ProjectManager.EditingProject = new Project()
            {
                Chart = DebuggingUtils.CreateDemoChart2()
            };
            ProjectManager.EditingProjectFilePath = null;
            WeakReferenceMessenger.Default.Send(new ProjectLoadedMessage());
        }

        [RelayCommand]
        private void ApplyBezierToSelected()
        {
            // 1. 抓取当前 BezierCurveEditor 里的 4 个参数，实例化一个干净的底层结构体
            var newEasing = new BezierEasing(
                BezierViewModel.X1,
                BezierViewModel.Y1,
                BezierViewModel.X2,
                BezierViewModel.Y2
            );

            bool hasModified = false;

            // 2. 扫荡 BPM 轨道
            if (Timeline.BpmTrack != null)
            {
                var selectedBpm = Timeline.BpmTrack.UIBpmKeyframes.Where(k => k.IsSelected);
                foreach (var wrapper in selectedBpm)
                {
                    wrapper.Model.Easing = newEasing; // 直接修改底层纯净数据
                    hasModified = true;
                }
            }

            // 3. 扫荡所有判定线图层，以及里面的音符！
            foreach (var track in Timeline.Tracks)
            {
                // ================= A. 判定线自己的关键帧 =================
                // Offset
                foreach (var wrapper in track.UIOffsetKeyframes.Where(k => k.IsSelected))
                {
                    wrapper.Model.Easing = newEasing;
                    hasModified = true;
                }

                // Scale
                foreach (var wrapper in track.UIScaleKeyframes.Where(k => k.IsSelected))
                {
                    wrapper.Model.Easing = newEasing;
                    hasModified = true;
                }

                // Rotation
                foreach (var wrapper in track.UIRotationKeyframes.Where(k => k.IsSelected))
                {
                    wrapper.Model.Easing = newEasing;
                    hasModified = true;
                }

                // Opacity
                foreach (var wrapper in track.UIOpacityKeyframes.Where(k => k.IsSelected))
                {
                    wrapper.Model.Easing = newEasing;
                    hasModified = true;
                }

                // ================= B. 新增：扫荡音符自己的关键帧 =================
                foreach (var note in track.UINotes)
                {
                    // Note Offset
                    foreach (var wrapper in note.UIOffsetKeyframes.Where(k => k.IsSelected))
                    {
                        wrapper.Model.Easing = newEasing;
                        hasModified = true;
                    }

                    // Note Scale
                    foreach (var wrapper in note.UIScaleKeyframes.Where(k => k.IsSelected))
                    {
                        wrapper.Model.Easing = newEasing;
                        hasModified = true;
                    }

                    // Note Rotation
                    foreach (var wrapper in note.UIRotationKeyframes.Where(k => k.IsSelected))
                    {
                        wrapper.Model.Easing = newEasing;
                        hasModified = true;
                    }

                    // Note Opacity
                    foreach (var wrapper in note.UIOpacityKeyframes.Where(k => k.IsSelected))
                    {
                        wrapper.Model.Easing = newEasing;
                        hasModified = true;
                    }
                }
            }

            // 4. 如果确实修改了数据，发信让右侧播放器/渲染器重绘！
            if (hasModified)
            {
                // 强制暂停一下，防止渲染器一边算一边改导致数值抖动
                WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());

                // 通知渲染器重绘画面
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
        }

    }
}
