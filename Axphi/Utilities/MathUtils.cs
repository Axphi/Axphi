using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axphi.Utilities
{
    internal static class MathUtils
    {
        public static double Lerp(double start, double end, double t)
        {
            return start + (end - start) * t;
        }
    }
}
