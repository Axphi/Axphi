using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;
using Axphi.Data;

namespace Axphi.ViewModels
{
    public partial class TrackViewModel : ObservableObject
    {
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
        private double _currentOpacity = 1.0; // 默认透明度给 1

        // ================= 4. 构造函数 =================
        public TrackViewModel(JudgementLine data, string name)
        {
            Data = data;
            TrackName = name;
        }

        // ================= 5. 核心拦截器 (黑魔法) =================
        // 当你在界面上按住 DraggableValueBox 左右拖拽时，这个方法会被疯狂触发！
        partial void OnCurrentOffsetXChanged(double value)
        {
            // TODO: 我们下一阶段的核心逻辑将写在这里：
            // 1. 获取当前时间轴播放到了哪一秒 (Tick)
            // 2. 去 Data 里面找当前时间有没有关键帧
            // 3. 如果有，直接修改那个关键帧的 X 值
            // 4. 如果没有，就 new 一个新的关键帧塞进去！

            // 可以先打个日志测试一下
            System.Diagnostics.Debug.WriteLine($"轨道 {TrackName} 的 OffsetX 被拖拽成了: {value}");
        }

        // （未来其他属性的拦截器也是同理，比如 partial void OnCurrentOffsetYChanged...）
    }
}

