using System;
using System.IO;

namespace zzio.primitives
{
    [Serializable]
    public struct ForeignKey
    {
        public readonly UID uid, type;

        public ForeignKey(UID uid = new UID(), UID type = new UID())
        {
            this.uid = uid;
            this.type = type;
        }

        public override int GetHashCode()
        {
            return (type.raw.GetHashCode() << 4) ^ 0x15fba3ce ^ uid.raw.GetHashCode();
        }

        public static ForeignKey ReadNew(BinaryReader reader)
        {
            return new ForeignKey(UID.ReadNew(reader), UID.ReadNew(reader));
        }

        public void Write(BinaryWriter writer)
        {
            uid.Write(writer);
            type.Write(writer);
        }

        public override string ToString()
        {
            return uid.ToString() + "|" + type.ToString();
        }
    }
}
