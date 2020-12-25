using System;
using System.Collections.Generic;
using System.Text;

namespace zzre
{
    public static class MathEx
    {
        public static bool Cmp(float a, float b) =>
            Math.Abs(a - b) <= float.Epsilon * Math.Max(1f, Math.Max(Math.Abs(a), Math.Abs(b)));

        public static bool CmpZero(float a) => Cmp(a, 0.0f);
    }
}
