using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace zzio.cli
{
    //first 4B * 36 + 20 = size
    public class ScannerSKA : IFileScanner
    {
        public FileType ScannerType { get { return FileType.SKA; } }
        public bool mightBe(Stream stream)
        {
            if (stream.Length < 20)
                return false;
            BinaryReader reader = new BinaryReader(stream);
            return reader.ReadUInt32() * 36 + 20 == (uint)stream.Length;
        }
    }

    //4B Magic 16 X X 1 4
    public class ScannerDFF : IFileScanner
    {
        public FileType ScannerType { get { return FileType.RWBS_DFF; } }
        public bool mightBe(Stream stream)
        {
            if (stream.Length < 20)
                return false;
            BinaryReader reader = new BinaryReader(stream);
            uint first = reader.ReadUInt32();
            stream.Seek(8, SeekOrigin.Current);
            return first == 16 && reader.ReadUInt32() == 1 && reader.ReadUInt32() == 4;
        }
    }

    //4B Magic 11 X X 1 52
    public class ScannerBSP : IFileScanner
    {
        public FileType ScannerType { get { return FileType.RWBS_BSP; } }
        public bool mightBe(Stream stream)
        {
            if (stream.Length < 20)
                return false;
            BinaryReader reader = new BinaryReader(stream);
            uint first = reader.ReadUInt32();
            stream.Seek(8, SeekOrigin.Current);
            return first == 11 && reader.ReadUInt32() == 1 && reader.ReadUInt32() == 52;
        }
    }

    //first 4B 17, first zstring "[Effect Combiner]"
    public class ScannerED : IFileScanner
    {
        public FileType ScannerType { get { return FileType.ED; } }
        public bool mightBe(Stream stream)
        {
            if (stream.Length < 24)
                return false;
            BinaryReader reader = new BinaryReader(stream);
            uint first = reader.ReadUInt32();
            if (first != 17)
                return false;
            return reader.ReadSizedString(first) == "[Effect Combiner]";
        }
    }

    //first 4B 11, first zstring [Scenefile]
    public class ScannerSCN : IFileScanner
    {
        public FileType ScannerType { get { return FileType.SCN; } }
        public bool mightBe(Stream stream)
        {
            if (stream.Length < 24)
                return false;
            BinaryReader reader = new BinaryReader(stream);
            uint first = reader.ReadUInt32();
            if (first != 11)
                return false;
            return reader.ReadSizedString(first) == "[Scenefile]";
        }
    }

    //first 4B 24, first zstring [ActorExDescriptionFile]
    public class ScannerAED : IFileScanner
    {
        public FileType ScannerType { get { return FileType.AED; } }
        public bool mightBe(Stream stream)
        {
            if (stream.Length < 24)
                return false;
            BinaryReader reader = new BinaryReader(stream);
            uint first = reader.ReadUInt32();
            if (first != 24)
                return false;
            return reader.ReadSizedString(first) == "[ActorExDescriptionFile]";
        }
    }

    //skip 19B, read byte sized, XORed 0x75 string with regex
    public class ScannerCFG_Vars : IFileScanner
    {
        public FileType ScannerType { get { return FileType.CFG_Vars; } }
        public bool mightBe(Stream stream)
        {
            if (stream.Length < 21)
                return false;
            BinaryReader reader = new BinaryReader(stream);
            stream.Seek(25, SeekOrigin.Current);
            uint size = reader.ReadByte();
            if (stream.Length < 20 + size)
                return false;
            var str = new StringBuilder(reader.ReadSizedString(size));
            for (int i = 0; i < size; i++)
                str[i] = (char)(str[i] ^ 0x75);
            return Regex.Match(str.ToString(), "^[A-Za-z0-9_]+$").Success;
        }
    }

    //first 4B * 16 + 4 = size
    public class ScannerCFG_Map : IFileScanner
    {
        public FileType ScannerType { get { return FileType.CFG_Map; } }
        public bool mightBe(Stream stream)
        {
            if (stream.Length < 4)
                return false;
            BinaryReader reader = new BinaryReader(stream);
            return reader.ReadUInt32() * 16 + 4 == (uint)stream.Length;
        }
    }

    //first 4B < 2^31, skip 4B, next 4B < 512, zstring with regex
    public class ScannerFBS_Index : IFileScanner
    {
        public FileType ScannerType { get { return FileType.FBS_Index; } }
        public bool mightBe(Stream stream)
        {
            if (stream.Length < 24)
                return false;
            BinaryReader reader = new BinaryReader(stream);
            if (reader.ReadUInt32() >= 1 << 30)
                return false;
            reader.ReadUInt32(); //skip
            uint size = reader.ReadUInt32();
            if (size > 512 || size == 0) //this is just a guess for the normal FBS index
                return false;
            return Regex.Match(reader.ReadSizedString(size), "^[A-Za-z0-9_]+$").Success;
        }
    }

    //first 4B < 2^31, skip 4B, next 4B < 512, next 4B < 5, not a very good scanner....
    public class ScannerFBS_Data : IFileScanner
    {
        public FileType ScannerType { get { return FileType.FBS_Data; } }
        public bool mightBe(Stream stream)
        {
            if (stream.Length < 24)
                return false;
            BinaryReader reader = new BinaryReader(stream);
            if (reader.ReadUInt32() >= 1 << 30)
                return false;
            reader.ReadUInt32(); //skip
            if (reader.ReadUInt32() > 512)
                return false;
            if (reader.ReadUInt32() > 4)
                return false;
            return true;
        }
    }

    public partial class ConversionMgr
    {
        private static IFileScanner[] scanners =
        {
            new ScannerSKA(),
            new ScannerDFF(),
            new ScannerBSP(),
            new ScannerED(),
            new ScannerSCN(),
            new ScannerAED(),
            new ScannerCFG_Vars(),
            new ScannerCFG_Map(),
            new ScannerFBS_Index(),
            new ScannerFBS_Data()
        };
    }
}
