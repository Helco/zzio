using System;
using System.IO;
using System.Text;

namespace zzio.utils
{
    public static class BinaryIOExtension
    {
        private static readonly Encoding encoding = Encoding.GetEncoding("Latin1");

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
            return encoding.GetString(buf).Replace("\u0000", "");
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
            return len == 0 ? "" : encoding.GetString(buf, 0, len);
        }

        /// <summary>Writes a 32-bit prefixed, not-0-terminated string</summary>
        public static void WriteZString(this BinaryWriter writer, string text)
        {
            byte[] buf = encoding.GetBytes(text);
            writer.Write(buf.Length);
            writer.WriteSizedString(buf, buf.Length);
        }

        /// <summary>Writes a 32-bit prefixed, 0-terminated string</summary>
        public static void WriteTZString(this BinaryWriter writer, string text)
        {
            byte[] buf = encoding.GetBytes(text);
            writer.Write(buf.Length + 1);
            writer.WriteSizedString(buf, buf.Length + 1);
        }

        /// <summary>Writes a fixed sized, 0-terminated string</summary>
        /// <param name="maxLen">Max byte count to write, including the 0-terminator</param>
        public static void WriteSizedCString(this BinaryWriter writer, String text, int maxLen)
        {
            writer.WriteSizedString(encoding.GetBytes(text), maxLen - 1);
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
    }
}
