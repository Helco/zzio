using System;
using System.IO;
using Newtonsoft.Json;
using zzio.primitives;

namespace zzio {
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