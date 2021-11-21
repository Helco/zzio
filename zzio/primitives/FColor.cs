using System.IO;

namespace zzio
{
    [System.Serializable]
    public struct FColor
    {
        public float r, g, b, a;

        public FColor(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static FColor ReadNew(BinaryReader r)
        {
            FColor c;
            c.r = r.ReadSingle();
            c.g = r.ReadSingle();
            c.b = r.ReadSingle();
            c.a = r.ReadSingle();
            return c;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(r);
            w.Write(g);
            w.Write(b);
            w.Write(a);
        }

        public static FColor operator *(FColor a, FColor b) => new FColor(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

        public static FColor White => new FColor(1.0f, 1.0f, 1.0f, 1.0f);
        public static FColor Black => new FColor(0.0f, 0.0f, 0.0f, 1.0f);
        public static FColor Clear => new FColor(0.0f, 0.0f, 0.0f, 0.0f);
        public static FColor Red => new FColor(1.0f, 0.0f, 0.0f, 1.0f);
        public static FColor Green => new FColor(0.0f, 1.0f, 0.0f, 1.0f);
        public static FColor Blue => new FColor(0.0f, 0.0f, 1.0f, 1.0f);
    }
}
