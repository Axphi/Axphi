using System.Windows;
using Axphi.Data.Abstraction;

namespace Axphi.Data.KeyFrames
{
    public class ScaleKeyFrame<TParent> : KeyFrame<TParent, Vector>, IVectorKeyFrame
        where TParent : class
    {
        public ScaleKeyFrame() { Value = new Vector(1, 1); }
    }
}
