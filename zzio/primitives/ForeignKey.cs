using System;
using System.Collections.Generic;
using System.IO;

namespace zzio.primitives
{
    [Serializable]
    public struct ForeignKey : IEquatable<ForeignKey>
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

        public override string ToString() => $"{uid}|{type}";

        public override bool Equals(object? obj) => obj is ForeignKey key && Equals(key);
        public bool Equals(ForeignKey other) => uid == other.uid && type == other.type;
        public override int GetHashCode() => HashCode.Combine(uid, type);
        public static bool operator ==(ForeignKey left, ForeignKey right) => left.Equals(right);
        public static bool operator !=(ForeignKey left, ForeignKey right) => !(left == right);
    }
}
