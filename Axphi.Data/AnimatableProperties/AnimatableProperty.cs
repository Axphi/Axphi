using Axphi.Data.KeyFrames;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axphi.Data.AnimatableProperties
{
    public partial class AnimatableProperty<T, TKeyFrame> : ObservableObject
        where T : struct
        where TKeyFrame : KeyFrame<T>
    {
        public T InitialValue { get; set; }

        public ObservableCollection<TKeyFrame> KeyFrames { get; } = new();
    }
}
