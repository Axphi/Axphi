using System.Windows;
using Axphi.Data.Abstraction;

namespace Axphi.Data.KeyFrames
{
    public class OffsetKeyFrame<TParent> : KeyFrame<TParent, Vector>, IVectorKeyFrame
        where TParent : class;
}
