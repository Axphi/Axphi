using Axphi.Data;

namespace Axphi.Utilities
{
    /// <summary>
    /// 贝塞尔缓动曲线预设库
    /// </summary>
    public static class BezierPresets
    {
        // 直接利用我们刚刚写的构造函数，代码极其简练
        public static readonly BezierEasing Linear = new BezierEasing(0.5, 0.5, 0.5, 0.5);
        public static readonly BezierEasing Ease = new BezierEasing(0.5, 0.0, 0.5, 1.0);

        // 以后你甚至可以加上各种标准缓动
        public static readonly BezierEasing EaseIn = new BezierEasing(0.42, 0.0, 1.0, 1.0);
        public static readonly BezierEasing EaseOut = new BezierEasing(0.0, 0.0, 0.58, 1.0);
        public static readonly BezierEasing EaseInOut = new BezierEasing(0.42, 0.0, 0.58, 1.0);
    }
}