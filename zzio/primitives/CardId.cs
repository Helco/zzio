using System;
using zzio.utils;

namespace zzio.primitives
{
    public enum CardType
    {
        Item = 0,
        Spell = 1,
        Fairy = 2,

        Unknown = -1
    }

    [Serializable]
    public struct CardId
    {
        public readonly UInt32 raw;

        public CardType Type => EnumUtils.intToEnum<CardType>((int)(raw >> 8) & 0xff);
        public int EntityId => (int)(raw >> 16);
        public int UnknownValidation => (int)(raw & 0xff);

        public CardId(UInt32 raw) {
            this.raw = raw;
        }

        public CardId(int raw) {
            this.raw = (UInt32)raw;
        }

        public CardId(CardType type, int entityId)
        {
            if (type == CardType.Unknown)
                throw new InvalidOperationException("Invalid CardType");
            if (entityId < 0 || entityId > UInt16.MaxValue)
                throw new InvalidOperationException("Invalid EntityId");
            raw =
                ((uint)type << 8) |
                ((uint)entityId << 16);
        }

        public override int GetHashCode()
        {
            return raw.GetHashCode();
        }

        public override string ToString()
        {
            return Type.ToString() + ":" + EntityId;
        }
    }
}
