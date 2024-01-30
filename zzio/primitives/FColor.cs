using System.IO;

namespace zzio;

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

    public static FColor operator *(FColor a, FColor b) => new(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

    public static FColor operator *(FColor a, float f) => new(a.r * f, a.g * f, a.b * f, a.a * f);

    public static implicit operator IColor(FColor c) => new(
        (byte)(c.r * 255f),
        (byte)(c.g * 255f),
        (byte)(c.b * 255f),
        (byte)(c.a * 255f));

    public static FColor White => new(1.0f, 1.0f, 1.0f, 1.0f);
    public static FColor Black => new(0.0f, 0.0f, 0.0f, 1.0f);
    public static FColor Clear => new(0.0f, 0.0f, 0.0f, 0.0f);
    public static FColor Red => new(1.0f, 0.0f, 0.0f, 1.0f);
    public static FColor Green => new(0.0f, 1.0f, 0.0f, 1.0f);
    public static FColor Blue => new(0.0f, 0.0f, 1.0f, 1.0f);
}
