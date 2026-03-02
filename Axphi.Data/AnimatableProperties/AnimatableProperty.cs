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
        where TKeyFrame : KeyFrame<T>, new() // 需要 new() 约束来创建幽灵帧
    {
        

        // 兜底默认值（当一个关键帧都没有时使用，比如位移默认0，缩放默认1）
        public T FallbackValue { get; }

        

        // 1. 供 UI 绑定的列表：纯粹由“用户亲手打上去”的关键帧
        public ObservableCollection<TKeyFrame> KeyFrames { get; }

        // 2. 供底层渲染/EasingUtils 使用的列表：包含了“幽灵帧”的完整列表
        private readonly List<TKeyFrame> _renderKeyFrames = new();
        public IReadOnlyList<TKeyFrame> RenderKeyFrames => _renderKeyFrames;

        public AnimatableProperty(T fallbackValue)
        {
            FallbackValue = fallbackValue;
            KeyFrames = new ObservableCollection<TKeyFrame>();

            // 监听用户列表的任何增删改变化
            KeyFrames.CollectionChanged += OnKeyFramesChanged;

            // 初始化时立刻生成一次渲染列表
            UpdateRenderKeyFrames();
        }

        private void OnKeyFramesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateRenderKeyFrames();
        }

        // 核心魔法：根据你的 4 条规则，动态生成渲染列表
        private void UpdateRenderKeyFrames()
        {
            _renderKeyFrames.Clear();

            // 规则2：如果用户完全没有打关键帧
            if (KeyFrames.Count == 0)
            {
                // 生成一个代码层面的 tick=0 幽灵帧，值为兜底值
                _renderKeyFrames.Add(new TKeyFrame { Time = 0, Value = FallbackValue });
                return;
            }

            // 假设用户的 KeyFrames 已经按时间排好序了
            var firstUserKf = KeyFrames.First();

            // 规则4：用户在 tick>0 放置了关键帧，且 tick=0 没有关键帧
            if (firstUserKf.Time > 0)
            {
                // 插入一个幽灵帧，时间为 0，值等于用户第一个关键帧的值！
                _renderKeyFrames.Add(new TKeyFrame
                {
                    Time = 0,
                    Value = firstUserKf.Value,
                    // 因为值一样，插值什么也无所谓，可以给个默认的线性曲线
                    Easing = BezierEasing.Linear
                });
            }

            // 规则3：如果用户在 tick=0 放了关键帧，上面的 if 不会触发，
            // 默认关键帧自然就被“覆盖/顶替”了。

            // 最后，把用户真实打的所有关键帧追加进去
            _renderKeyFrames.AddRange(KeyFrames);
        }
    }
}
