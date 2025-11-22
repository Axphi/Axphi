using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Axphi.Utilities
{
    internal static class MathUtils
    {
        public static double Lerp(double start, double end, double t)
        {
            return start + (end - start) * t;
        }

        public static Vector Lerp(Vector start, Vector end, double t)
        {
            return new Vector(
                Lerp(start.X, end.X, t),
                Lerp(start.Y, end.Y, t));
        }
    }
}
