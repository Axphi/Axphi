using System;
using Axphi.Data;

namespace Axphi.Utilities
{
    /// <summary>
    /// 贝塞尔曲线计算引擎 (纯静态函数，没有任何状态)
    /// </summary>
    public static class BezierCalculator
    {
        // 🌟 现在的入参变成了：(传入那张数据白纸, 传入时间 t)
        public static double Calculate(BezierEasing easing, double t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;

            double parameter = GetSampleRate(easing.X1, easing.X2, t);
            return GetSamplePoint(easing.Y1, easing.Y2, parameter);
        }

        public static double Calculate(BezierEasing easing, double start, double end, double t)
        {
            double y = Calculate(easing, t);
            return start + (end - start) * y;
        }

        private static double GetSamplePoint(double cp1, double cp2, double rate)
        {
            return 3d * cp1 * rate * (1d - rate) * (1d - rate) + 3d * cp2 * rate * rate * (1d - rate) + rate * rate * rate;
        }

        public static double GetSampleRate(double cp1, double cp2, double p)
        {
            double cx = 3d * cp1;
            double bx = 3d * (cp2 - cp1) - cx;
            double ax = 1d - cx - bx;

            const double NewtonEpsilon = 1e-7d;
            double u = p;

            // 1. 牛顿迭代
            for (int i = 0; i < 8; i++)
            {
                double currentX = ((ax * u + bx) * u + cx) * u - p;
                if (Math.Abs(currentX) < NewtonEpsilon)
                    return u;

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