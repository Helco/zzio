using System.IO;

namespace zzio;

[System.Serializable]
public struct IColor
{
    public byte r, g, b, a;

    public IColor(byte r, byte g, byte b, byte a = 255)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    public IColor(uint c)
    {
        r = (byte)(c >> 0 & 0xff);
        g = (byte)(c >> 8 & 0xff);
        b = (byte)(c >> 16 & 0xff);
        a = (byte)(c >> 24 & 0xff);
    }

    public readonly uint Raw =>
        ((uint)r << 0) |
        ((uint)g << 8) |
        ((uint)b << 16) |
        ((uint)a << 24);

    public static IColor ReadNew(BinaryReader r)
    {
        IColor c;
        c.r = r.ReadByte();
        c.g = r.ReadByte();
        c.b = r.ReadByte();
        c.a = r.ReadByte();
        return c;
    }

    public void Write(BinaryWriter w)
    {
        w.Write(r);
        w.Write(g);
        w.Write(b);
        w.Write(a);
    }

    public FColor ToFColor() => new(r / 255f, g / 255f, b / 255f, a / 255f);

    public IColor WithA(byte newAlpha) => new(r, g, b, newAlpha);
    public static implicit operator FColor(IColor c) => new(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);

    public static readonly IColor White = new(0xFFFFFFFF);
    public static readonly IColor Black = new(0xFF000000);
    public static readonly IColor Clear = new(0x00000000);
    public static readonly IColor Red = new(0xFF0000FF);
    public static readonly IColor Green = new(0xFF00FF00);
    public static readonly IColor Blue = new(0xFFFF0000);
}
