using System;
using System.IO;
using Newtonsoft.Json;

namespace zzio {
    [System.Serializable]
    public struct IColor
    {
        public byte r, g, b, a;

        public IColor(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static IColor read(BinaryReader r)
        {
            IColor c;
            c.r = r.ReadByte();
            c.g = r.ReadByte();
            c.b = r.ReadByte();
            c.a = r.ReadByte();
            return c;
        }

        public void write(BinaryWriter w)
        {
            w.Write(r);
            w.Write(g);
            w.Write(b);
            w.Write(a);
        }
    }

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

        public static FColor read(BinaryReader r)
        {
            FColor c;
            c.r = r.ReadSingle();
            c.g = r.ReadSingle();
            c.b = r.ReadSingle();
            c.a = r.ReadSingle();
            return c;
        }

        public void write(BinaryWriter w)
        {
            w.Write(r);
            w.Write(g);
            w.Write(b);
            w.Write(a);
        }
    }

    [System.Serializable]
    public struct Vector {
        public float x, y, z;

        public Vector(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector read(BinaryReader r) {
            Vector v;
            v.x = r.ReadSingle();
            v.y = r.ReadSingle();
            v.z = r.ReadSingle();
            return v;
        }

        public void write(BinaryWriter w)
        {
            w.Write(x);
            w.Write(y);
            w.Write(z);
        }
    }

    [System.Serializable]
    public struct Quaternion {
        public float x, y, z, w;

        public static Quaternion read(BinaryReader r) {
            Quaternion q;
            q.x = r.ReadSingle();
            q.y = r.ReadSingle();
            q.z = r.ReadSingle();
            q.w = r.ReadSingle();
            return q;
        }

        public void write(BinaryWriter w)
        {
            w.Write(x);
            w.Write(y);
            w.Write(z);
            w.Write(this.w);
        }
    }

    [System.Serializable]
    public struct TexCoord {
        public float u, v;

        public static TexCoord read(BinaryReader r) {
            TexCoord t;
            t.u = r.ReadSingle();
            t.v = r.ReadSingle();
            return t;
        }

        public void write(BinaryWriter w)
        {
            w.Write(u);
            w.Write(v);
        }
    }

    [System.Serializable]
    public struct Triangle {
        public UInt16 m, v1, v2, v3;

        public static Triangle read(BinaryReader r) {
            Triangle t;
            t.m = r.ReadUInt16();
            t.v1 = r.ReadUInt16();
            t.v2 = r.ReadUInt16();
            t.v3 = r.ReadUInt16();
            return t;
        }

        public void write(BinaryWriter w)
        {
            w.Write(m);
            w.Write(v1);
            w.Write(v2);
            w.Write(v3);
        }
    }

    [System.Serializable]
    public struct Normal {
        public byte x, y, z;
        public sbyte p;

        public static Normal read(BinaryReader r) {
            Normal n;
            n.x = r.ReadByte();
            n.y = r.ReadByte();
            n.z = r.ReadByte();
            n.p = r.ReadSByte();
            return n;
        }

        public void write(BinaryWriter w)
        {
            w.Write(x);
            w.Write(y);
            w.Write(z);
            w.Write(p);
        }
    }

    namespace rwbs {
        [System.Serializable]
        public struct Frame {
            public float[] rotMatrix;
            public Vector position;
            public UInt32 frameIndex; //propably previous sibling?
            public UInt32 creationFlags;

            public static Frame read(BinaryReader reader) {
                Frame f;
                f.rotMatrix = new float[9];
                for (int i = 0; i < 9; i++)
                    f.rotMatrix[i] = reader.ReadSingle();
                f.position = Vector.read(reader);
                f.frameIndex = reader.ReadUInt32();
                f.creationFlags = reader.ReadUInt32();
                return f;
            }

            public void write(BinaryWriter w)
            {
                for (int i = 0; i < 9; i++)
                    w.Write(rotMatrix[i]);
                position.write(w);
                w.Write(frameIndex);
                w.Write(creationFlags);
            }
        }
    }

    public partial class Utils {
        public static string readZString(BinaryReader reader) {
            return readSizedString(reader, reader.ReadUInt32());
        }

        internal static T intToFlags<T>(uint v)
        {
            throw new NotImplementedException();
        }

        public static string readSizedString(BinaryReader reader, uint len)
        {
            if (len == 0)
                return "";
            byte[] buf = reader.ReadBytes((int)len);
            return System.Text.Encoding.Default.GetString(buf).Replace("\u0000", "");
        }

        public static void writeZString(BinaryWriter writer, String text)
        {
            byte[] buf = System.Text.Encoding.Default.GetBytes(text);
            writeSizedString(writer, buf, buf.Length + 1); //zstrings have one zero byte at the end
        }

        public static void writeSizedString(BinaryWriter writer, String text, int len)
        {
            writeSizedString(writer, System.Text.Encoding.Default.GetBytes(text), len);
        }

        public static void writeSizedString(BinaryWriter writer, byte[] buf, int len)
        {
            writer.Write(buf, 0, Math.Min(buf.Length, len));
            if (len > buf.Length) {
                uint rest = (uint)(len - buf.Length);
                while (rest >= 8)
                {
                    writer.Write((ulong)0);
                    rest -= 8;
                }
                if (rest >= 4)
                {
                    writer.Write((uint)0);
                    rest -= 4;
                }
                while (rest-- < 4) //use the integer underflow to our favor
                    writer.Write((byte)0);
            }
        }

        public static T intToEnum<T>(int i) {
            if (Enum.IsDefined(typeof(T), i))
                return (T)Enum.Parse(typeof(T), i.ToString());
            else
                return (T)Enum.Parse(typeof(T), "Unknown");
        }

        public static bool isUID (string str)
        {
            if (str.Length > 8)
                return false;
            for (int i=0; i<str.Length; i++)
            {
                if (!(str[i] >= '0' && str[i] <= '9') &&
                    !(str[i] >= 'A' && str[i] <= 'F') &&
                    !(str[i] >= 'a' && str[i] <= 'f'))
                    return false;
            }
            return true;
        }

        //read string as a char array (exactly n chars, but string ends with the first 0 character or end)
        public static string readCAString(BinaryReader r, int n)
        {
            byte[] buf = r.ReadBytes(n);
            int len = 0;
            for (; len<n; len++)
            {
                if (buf[len] == 0)
                    break;
            }
            if (len == 0)
                return "";
            else
                return System.Text.Encoding.Default.GetString(buf, 0, len);
        }
    }
}