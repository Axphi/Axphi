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

        public static readonly BezierEasing Linear = new BezierEasing()
        {
            X1 = 0.5,
            Y1 = 0.5,
            X2 = 0.5,
            Y2 = 0.5
        };

        public static readonly BezierEasing Ease = new BezierEasing()
        {
            X1 = 0.5,
            Y1 = 0.0,
            X2 = 0.5,
            Y2 = 1.0
        };

        public double Calculate(double t)
        {
            // 边界情况处理
            if (t <= 0) return 0;
            if (t >= 1) return 1;

            // 找到对应时间 t 的贝塞尔参数
            double parameter = GetSampleRate(X1, X2, t);

            // 计算对应的 y 值 (使用贝塞尔曲线公式)
            double y = GetSamplePoint(Y1, Y2, parameter);

            return y;
        }

        public double Calculate(double start, double end, double t)
        {
            // 计算对应的 y
            double y = Calculate(t);

            // 映射到目标区间
            return start + (end - start) * y;
        }

        private static double GetSamplePoint(double cp1, double cp2, double rate)
        {
            return 3d * cp1 * rate * (1d - rate) * (1d - rate) + 3d * cp2 * rate * rate * (1d - rate) + rate * rate * rate;
            // return 3 * cp1 * rate - 3 * cp1 * 2 * rate * rate + 3 * cp1 * rate * rate * rate + 3 * cp2 * rate * rate - 3 * cp2 * rate * rate * rate + rate * rate * rate;
        }

        



        // 传入 cp1, cp2, p
        // 求解: ( x1 = cp1, x2 = cp2, t = p )
        // 已知:
        // x(u) = 3(1-u)^2 u * x1 + 3(1-u) u^2 * x2 + u^3
        // x(u) = t
        // 求 u
        public static double GetSampleRate(double cp1, double cp2, double p)
        {
            // 提前计算多项式系数，取代之前的重复传参计算
            double cx = 3d * cp1;
            double bx = 3d * (cp2 - cp1) - cx;
            double ax = 1d - cx - bx;

            const double NewtonEpsilon = 1e-7d;
            double u = p;

            // 1. 牛顿迭代
            for (int i = 0; i < 8; i++)
            {
                // 极致内联展开：直接计算当前 X 和误差
                double currentX = ((ax * u + bx) * u + cx) * u - p;
                if (Math.Abs(currentX) < NewtonEpsilon)
                    return u;

                // 极致内联展开：计算斜率
                double currentSlope = (3d * ax * u + 2d * bx) * u + cx;
                if (Math.Abs(currentSlope) < 0.001d)
                    break;

                u -= currentX / currentSlope;
            }

            // 2. 二分法兜底
            double t0 = 0.0d;
            double t1 = 1.0d;
            u = p;

            if (u < t0) return t0;
            if (u > t1) return t1;

            while (t0 < t1)
            {
                double currentX = ((ax * u + bx) * u + cx) * u;
                if (Math.Abs(currentX - p) < NewtonEpsilon)
                    return u;

                if (p > currentX) t0 = u;
                else t1 = u;

                u = (t1 - t0) * 0.5d + t0;
            }

            return u;
        }
    }

}
