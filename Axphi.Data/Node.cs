using Axphi.Data.KeyFrames;

namespace Axphi.Data
{
    /// <summary>
    /// 音符
    /// </summary>
    public class Node
    {
        public Node()
        {
        }

        public Node(NodeKind kind, TimeSpan hitTime)
        {
            Kind = kind;
            HitTime = hitTime;
        }

        public NodeKind Kind { get; set; }
        public TimeSpan HitTime { get; set; }
        public TimeSpan HoldDuration { get; set; }
        public double? CustomSpeed { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public TransformKeyFrames? TransformKeyFrames { get; set; }
    }
}
