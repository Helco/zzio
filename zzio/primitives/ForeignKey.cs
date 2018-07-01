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

        public static ForeignKey ReadNew(BinaryReader reader)
        {
            return new ForeignKey(UID.ReadNew(reader), UID.ReadNew(reader));
        }

        public void Write(BinaryWriter writer)
        {
            uid.Write(writer);
            type.Write(writer);
        }
    }
}
