using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class OffsetProperty : AnimatableProperty<Vector, OffsetKeyFrame>
    {
        // 构造函数，利用 base 关键字把默认值传给父类
        public OffsetProperty()
        {
            InitialValue = new Vector(0,0);
        }
    }
    

}
