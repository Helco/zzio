using System;

namespace zzio;

public enum CardType
{
    Item = 0,
    Spell = 1,
    Fairy = 2,

    Unknown = -1
}

[Serializable]
public struct CardId : IEquatable<CardId>
{
    public readonly uint raw;

    public CardType Type => EnumUtils.intToEnum<CardType>((int)(raw >> 8) & 0xff);
    public int EntityId => (int)(raw >> 16);
    public int UnknownValidation => (int)(raw & 0xff);

    public CardId(uint raw)
    {
        this.raw = raw;
    }

    public CardId(int raw)
    {
        this.raw = unchecked((uint)raw);
    }

    public CardId(CardType type, int entityId)
    {
        if (type == CardType.Unknown)
            throw new InvalidOperationException("Invalid CardType");
        if (entityId < 0 || entityId > ushort.MaxValue)
            throw new InvalidOperationException("Invalid EntityId");
        raw =
            (uint)type << 8 |
            (uint)entityId << 16;
    }

    public override string ToString() => $"{Type}:{EntityId}";

    public override bool Equals(object? obj) => obj is CardId id && Equals(id);
    public bool Equals(CardId other) => raw == other.raw;
    public override int GetHashCode() => HashCode.Combine(raw);
    public static bool operator ==(CardId left, CardId right) => left.Equals(right);
    public static bool operator !=(CardId left, CardId right) => !(left == right);
}
