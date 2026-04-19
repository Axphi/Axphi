using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Axphi.Utilities;

namespace Axphi.ViewModels
{
    public abstract partial class TrackPropertyViewModelBase : ObservableObject
    {
        // 属性名称 (如 "Position", "Speed")
        [ObservableProperty]
        private string _name;

        // 当前时间点是否已经有关键帧 (用于UI菱形按钮高亮)
        [ObservableProperty]
        private bool _isKeyframed;

        protected TrackPropertyViewModelBase(string name)
        {
            _name = name;
        }


        // 允许子类重写具体打关键帧的逻辑
        [RelayCommand]
        protected virtual void AddKeyframe()
        {
            // TODO: 获取当前 Tick 并在底层模型中添加/移除关键帧
            // 示例伪代码:
            // var currentTick = PlayheadService.CurrentTick;
            // var existingFrame = GetFrameAt(currentTick);
            // if (existingFrame != null) RemoveFrame(existingFrame);
            // else AddFrame(new KeyFrame { Tick = currentTick, Value = GetCurrentValue() });

            // 触发后更新UI状态
            // IsKeyframed = true; 
        }
    }

    public partial class VectorPropertyViewModel : TrackPropertyViewModelBase
    {
        
        private readonly Property<Vector> _model;

        
        public VectorPropertyViewModel(string name, Property<Vector> model)
            : base(name)
        {
            _model = model;
        }

        // 必须手写 Getter/Setter 代理到底层的 InitialValue 结构体
        public double X
        {
            get => _model.InitialValue.X;
            set
            {
                var val = _model.InitialValue;
                if (val.X != value)
                {
                    val.X = value;
                    _model.InitialValue = val;
                    OnPropertyChanged(nameof(X));
                    WeakReferenceMessenger.Default.Send(new UpdateRendererMessage());
                }
            }
        }

        public double Y
        {
            get => _model.InitialValue.Y;
            set
            {
                var val = _model.InitialValue;
                if (val.Y != value)
                {
                    val.Y = value;
                    _model.InitialValue = val;
                    OnPropertyChanged(nameof(Y));
                    WeakReferenceMessenger.Default.Send(new UpdateRendererMessage());
                }
            }
        }
    }

    public partial class DoublePropertyViewModel : TrackPropertyViewModelBase
    {
        private readonly Property<double> _model;

        public DoublePropertyViewModel(string name, Property<double> model)
            : base(name)
        {
            _model = model;
        }

        // 针对 double，Getter/Setter 可以极其清爽
        public double Value
        {
            get => _model.InitialValue;
            set
            {
                // 直接比较，如果不等就赋值并触发 UI 更新
                if (_model.InitialValue != value)
                {
                    _model.InitialValue = value;
                    OnPropertyChanged(nameof(Value));
                    WeakReferenceMessenger.Default.Send(new UpdateRendererMessage());
                }
            }
        }
    }
}