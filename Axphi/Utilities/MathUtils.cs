using System.Windows;

namespace Axphi.Utilities;

internal static class MathUtils
{
    public static T Lerp<T>(T start, T end, double t) where T : 
        System.Numerics.ISubtractionOperators<T, T, T>,
        System.Numerics.IMultiplyOperators<T, double, T>,
        System.Numerics.IAdditionOperators<T, T, T>
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
