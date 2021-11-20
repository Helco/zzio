using System.Numerics;

namespace zzre.game.components
{
    public struct TexShift
    {
        public Matrix3x2 Matrix;

        public static readonly TexShift Default = new TexShift() { Matrix = Matrix3x2.Identity };
    }
}
