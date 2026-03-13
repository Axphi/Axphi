using System.Windows;

namespace Axphi.Data.KeyFrames
{
    public record class ScaleKeyFrame : KeyFrame<Vector>
    {
        public ScaleKeyFrame() { Value = new Vector(1, 1); }
    }

}
