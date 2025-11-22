using Axphi.Data.KeyFrames;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Axphi.Data.AnimatableProperties
{
    public partial class AnimatableProperty<T, TKeyFrame> : ObservableObject
        where T : struct
        where TKeyFrame : KeyFrame<T>
    {
        public T InitialValue { get; set; }

        public ObservableCollection<TKeyFrame> KeyFrames { get; } = new();
    }

    public sealed class OffsetProperty : AnimatableProperty<Vector, OffsetKeyFrame>;
    public sealed class ScaleProperty : AnimatableProperty<Vector, ScaleKeyFrame>
    {
        public ScaleProperty()
        {
            InitialValue = new Vector(1, 1);
        }
    }

    public sealed class RotationProperty : AnimatableProperty<double, RotationKeyFrame>;
    public sealed class OpacityProperty : AnimatableProperty<double, OpacityKeyFrame>
    {
        public OpacityProperty()
        {
            InitialValue = 1;
        }
    }
}
