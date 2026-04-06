using Axphi.Data.Abstraction;
using Axphi.Data.KeyFrames;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Axphi.Data.AnimatableProperties
{
    public partial class AnimatableProperty<TOwner, TSelf, TValue, TKeyFrame> : ObservableObject, IAnimatableProperty<TValue>
        where TOwner : class
        where TValue : struct
        where TSelf : AnimatableProperty<TOwner, TSelf, TValue, TKeyFrame>
        where TKeyFrame : KeyFrame<TSelf, TValue>, new()
    {
        [JsonIgnore]
        public TOwner Owner { get; }

        public TValue InitialValue { get; set; }

        public RelationObject<TSelf>.Collection<TKeyFrame> KeyFrames { get; }

        IReadOnlyList<IKeyFrame<TValue>> IAnimatableProperty<TValue>.KeyFrames => KeyFrames;

        public AnimatableProperty(TOwner owner)
        {
            Owner = owner;
            KeyFrames = new RelationObject<TSelf>.Collection<TKeyFrame>((TSelf)this);
        }

        public void AddKeyFrame(TimeSpan time, TValue value, BezierEasing? easing)
        {
            var keyFrame = new TKeyFrame
            {
                Time = time,
                Value = value,
                Easing = easing
            };

            KeyFrames.Add(keyFrame);
        }

        public bool RemoveKeyFrameByTime(TimeSpan time)
        {
            int index = -1;
            for (int i = 0; i < KeyFrames.Count; i++)
            {
                if (KeyFrames[i].Time == time)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                return false;
            }

            KeyFrames.RemoveAt(index);
            return true;
        }

        public bool RemoveKeyFrameByIndex(int index)
        {
            if (index < 0 ||
                index >= KeyFrames.Count)
            {
                return false;
            }

            KeyFrames.RemoveAt(index);
            return true;
        }
    }
}
