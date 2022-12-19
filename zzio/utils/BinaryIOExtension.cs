using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace zzio
{
    public static class BinaryIOExtension
    {
        public static readonly Encoding Encoding = Encoding.GetEncoding("Latin1");

        /// <summary>Reads a 32-bit size prefixed string</summary>
        public static string ReadZString(this BinaryReader reader)
        {
            return reader.ReadSizedString(reader.ReadInt32());
        }

        /// <summary>Reads a fixed-size string</summary>
        /// <remarks>Zero-bytes will be removed</summary>
        public static string ReadSizedString(this BinaryReader reader, int len)
        {
            if (len == 0)
                return "";
            byte[] buf = reader.ReadBytes(len);
            return Encoding.GetString(buf).Replace("\u0000", "");
        }

        /// <summary>Reads a fixed sized string</summary>
        /// <remarks>ignores everything after the first zero-byte</remarks>
        public static string ReadSizedCString(this BinaryReader reader, int maxLen)
        {
            byte[] buf = reader.ReadBytes(maxLen);
            int len = 0;
            for (; len < maxLen; len++)
            {
                if (buf[len] == 0)
                    break;
            }
            return len == 0 ? "" : Encoding.GetString(buf, 0, len);
        }

        public static unsafe void ReadStructureArray<T>(this BinaryReader reader, T[] array) where T : unmanaged
        {
            fixed (T* arrayPtr = array)
            {
                var span = new Span<byte>(arrayPtr, sizeof(T) * array.Length);
                var bytesRead = reader.Read(span);
                if (span.Length != bytesRead)
                    throw new EndOfStreamException($"Array {typeof(T)}[{array.Length}] could not be read completly");
            }
        }

        public static T[] ReadStructureArray<T>(this BinaryReader reader, int count) where T : unmanaged
        {
            var array = new T[count];
            reader.ReadStructureArray(array);
            return array;
        }

        public static Vector2 ReadVector2(this BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle());
        public static Vector3 ReadVector3(this BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        public static Vector4 ReadVector4(this BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        public static Quaternion ReadQuaternion(this BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        public static Matrix4x4 ReadMatrix4x4(this BinaryReader r) => new(
            r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(),
            r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(),
            r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(),
            r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        /// <summary>Writes a 32-bit prefixed, not-0-terminated string</summary>
        public static void WriteZString(this BinaryWriter writer, string text)
        {
            byte[] buf = Encoding.GetBytes(text);
            writer.Write(buf.Length);
            writer.WriteSizedString(buf, buf.Length);
        }

        /// <summary>Writes a 32-bit prefixed, 0-terminated string</summary>
        public static void WriteTZString(this BinaryWriter writer, string text)
        {
            byte[] buf = Encoding.GetBytes(text);
            writer.Write(buf.Length + 1);
            writer.WriteSizedString(buf, buf.Length + 1);
        }

        /// <summary>Writes a fixed sized, 0-terminated string</summary>
        /// <param name="maxLen">Max byte count to write, including the 0-terminator</param>
        public static void WriteSizedCString(this BinaryWriter writer, string text, int maxLen)
        {
            writer.WriteSizedString(Encoding.GetBytes(text), maxLen - 1);
            writer.Write((byte)0);
        }

        /// <summary>Writes a fixed sized, not terminated string</summary>
        /// <param name="maxLen">Max byte count to write</param>
        public static void WriteSizedString(this BinaryWriter writer, byte[] buf, int maxLen)
        {
            int written = Math.Min(buf.Length, maxLen);
            writer.Write(buf, 0, written);

            for (; written < maxLen; written++)
                writer.Write((byte)0);
        }

        public static unsafe void WriteStructureArray<T>(this BinaryWriter writer, T[] array) where T : unmanaged
        {
            fixed (T* arrayPtr = array)
            {
                writer.Write(new ReadOnlySpan<byte>(arrayPtr, sizeof(T) * array.Length));
            }
        }

        public static void Write(this BinaryWriter w, Vector2 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
        }

        public static void Write(this BinaryWriter w, Vector3 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
            w.Write(v.Z);
        }

        public static void Write(this BinaryWriter w, Vector4 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
            w.Write(v.Z);
            w.Write(v.W);
        }

        public static void Write(this BinaryWriter w, Quaternion v)
        {
            w.Write(v.X);
            w.Write(v.Y);
            w.Write(v.Z);
            w.Write(v.W);
        }

        public static void Write(this BinaryWriter w, Matrix4x4 m)
        {
            w.Write(m.M11);
            w.Write(m.M12);
            w.Write(m.M13);
            w.Write(m.M14);
            w.Write(m.M21);
            w.Write(m.M22);
            w.Write(m.M23);
            w.Write(m.M24);
            w.Write(m.M31);
            w.Write(m.M32);
            w.Write(m.M33);
            w.Write(m.M34);
            w.Write(m.M41);
            w.Write(m.M42);
            w.Write(m.M43);
            w.Write(m.M44);
        }
    }
}
