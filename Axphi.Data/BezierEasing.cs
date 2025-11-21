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

        private static double PickAppropriateRate(double cp1, double cp2, double p, params double[] rates)
        {
            double result = double.NaN;
            double diffNow = double.MaxValue;
            foreach (var rate in rates)
                if (!double.IsNaN(rate) && Math.Abs(GetSamplePoint(cp1, cp2, rate) - p) < diffNow)
                    result = rate;
            return result;
        }

        private static double MathCbrt(double num)
        {
            return num < 0 ? -Math.Pow(-num, 1d / 3) : Math.Pow(num, 1d / 3);
        }

        private static double GetSampleRate(double cp1, double cp2, double p)
        {
            double
                a = 3 * cp1 - 3 * cp2 + 1,
                b = -6 * cp1 + 3 * cp2,
                c = 3 * cp1,
                d = -p;
            double
                A = b * b - 3 * a * c,
                B = b * c - 9 * a * d,
                C = c * c - 3 * b * d;
            double
                delta = B * B - 4 * A * C;
            if (A == B)
            {
                double x = -c / b; // -b/3a -c/b -3d/c
                double rst = x;
                return rst;
            }
            else if (delta > 0)
            {
                //double I = double.NaN;
                double
                    y1 = A * b + 3 * a * ((-B + Math.Sqrt(delta)) / 2),
                    y2 = A * b + 3 * a * ((-B - Math.Sqrt(delta)) / 2);
                double
                    xtmp1 = MathCbrt(y1) + MathCbrt(y2); //,
                                                         //xtmp2 = Cubic(y1) - Cubic(y2);
                double   // what the fuck is I? virtual number wtf... imposible for now (((
                    x1 = (-b - xtmp1) / (3 * a); //,
                                                 //x2 = (-2 * b + xtmp1 + Math.Sqrt(3) * xtmp2 * I) / (6 * a),
                                                 //x3 = (-2 * b + xtmp1 - Math.Sqrt(3) * xtmp2 * I) / (6 * a);
                double rst = x1;
                return rst;
            }
            else if (delta == 0)
            {
                double k = B / A;
                double
                    x1 = -b / a + k,
                    x2 = -k / 2;
                double rst = PickAppropriateRate(cp1, cp2, p, x1, x2);
                return rst;
            }
            else  // delta < 0
            {
                double
                    t = (2 * A * b - 3 * a * B) / (2 * A * Math.Sqrt(A)),
                    sita = Math.Acos(t);
                double
                    x1 = (-b - 2 * Math.Sqrt(A) * Math.Cos(sita / 3)) / (3 * a),
                    x2 = (-b + Math.Sqrt(A) * (Math.Cos(sita / 3) + Math.Sqrt(3) * Math.Sin(sita / 3))) / (3 * a),
                    x3 = (-b + Math.Sqrt(A) * (Math.Cos(sita / 3) - Math.Sqrt(3) * Math.Sin(sita / 3))) / (3 * a);
                double rst = PickAppropriateRate(cp1, cp2, p, x1, x2, x3);
                return rst;
            }
        }
    }

}
