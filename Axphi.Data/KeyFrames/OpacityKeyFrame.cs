namespace Axphi.Data.KeyFrames
{
    public class OpacityKeyFrame<TParent> : KeyFrame<TParent, double>
        where TParent : class
    {
        public OpacityKeyFrame() { Value = 1; }
    }

}
