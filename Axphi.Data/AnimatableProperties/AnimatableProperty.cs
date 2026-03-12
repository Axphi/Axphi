using Axphi.Data.KeyFrames;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axphi.Data.AnimatableProperties
{


    //没有"初始值"
    //当用户未创建tick=0的关键帧时, 默认存在一个tick=0的关键帧, 这个关键帧就充当"初始值"
    //当某个属性的值从未出现过任何一个关键帧, 用户ui层面不显示这个关键帧,
    //但是代码层面(比如构造函数)上存在一个tick=0的关键帧
    //当用户在tick=0放置关键帧时, 覆盖此默认关键帧
    //当用户未在tick=0放置关键帧且在tick>0防止了关键帧,
    //那么此默认关键帧的值就等于tick最小且不等于0的关键帧,
    //插值为常值(值都一样了 插值是什么也无所谓了)

    public partial class AnimatableProperty<T, TKeyFrame> : ObservableObject
        where T : struct
        where TKeyFrame : KeyFrame<T>
    {
        // 待重构
        public T InitialValue { get; set; }

        public List<TKeyFrame> KeyFrames { get; } = new();


    }
}
