using System.Windows;

namespace Axphi.Data.KeyFrames
{
    public class ScaleKeyFrame<TParent> : KeyFrame<TParent, Vector>
        where TParent : class
    {
        public ScaleKeyFrame() { Value = new Vector(1, 1); }
    }
}
