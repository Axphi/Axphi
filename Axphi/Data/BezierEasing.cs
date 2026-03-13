namespace Axphi.Data
{
    /// <summary>
    /// 贝塞尔曲线缓动
    /// </summary>
    public struct BezierEasing
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        // 加上这个构造函数，以后写 new BezierEasing(0.25, 0.1, 0.25, 1) 会非常爽
        public BezierEasing(double x1, double y1, double x2, double y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }
    }

}
