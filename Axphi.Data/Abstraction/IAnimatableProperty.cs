namespace Axphi.Data.Abstraction
{
    public interface IAnimatableProperty<TValue>
    {
        public TValue InitialValue { get; set; }
        public IReadOnlyList<IKeyFrame<TValue>> KeyFrames { get; }
    }
}
