namespace Axphi.Data.KeyFrames
{
    // NoteKind 是枚举，属于值类型，完美契合 where T : struct 约束！
    public record class NoteKindKeyFrame : KeyFrame<NoteKind>
    {
    }

}
