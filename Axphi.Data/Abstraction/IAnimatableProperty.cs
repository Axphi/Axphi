namespace Axphi.Data.Abstraction
{
    public interface IAnimatableProperty<TValue>
    {
        public TValue InitialValue { get; set; }
        public IReadOnlyList<IKeyFrame<TValue>> KeyFrames { get; }

        public void AddKeyFrame(TimeSpan time, TValue value, BezierEasing? easing);
        public bool RemoveKeyFrameByTime(TimeSpan time);
        public bool RemoveKeyFrameByIndex(int index);
    }
}
