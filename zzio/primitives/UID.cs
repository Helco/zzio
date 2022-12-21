using System;
using System.IO;

namespace zzio;

[Serializable]
public struct UID : IEquatable<UID>
{
    public readonly uint raw;
    public int Module => (int)(raw % 16);

    public UID(uint raw = 0)
    {
        this.raw = raw;
    }

    public override int GetHashCode() => unchecked((int)raw);

    public static UID ReadNew(BinaryReader reader) => new(reader.ReadUInt32());
    public void Write(BinaryWriter writer) => writer.Write(raw);

    public static UID Parse(string text) => new(Convert.ToUInt32(text, 16));

    public override string ToString() => raw.ToString("X").PadLeft(8, '0');

    public override bool Equals(object? obj) => obj is UID uID && Equals(uID);
    public bool Equals(UID other) => raw == other.raw;
    public static bool operator ==(UID left, UID right) => left.Equals(right);
    public static bool operator !=(UID left, UID right) => !(left == right);
}
