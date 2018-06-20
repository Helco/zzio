using System;
using System.Collections.Generic;
using System.IO;
using zzio.utils;

namespace zzio {
    public class PAKArchive {
        private struct FilePos {
            public UInt32 offset, length;
        }
        private Dictionary<string, FilePos> files = new Dictionary<string, FilePos>();
        private Stream stream;
        private UInt32 baseOffset;

        private PAKArchive(Stream str) {
            stream = str;
        }

        public static PAKArchive read(Stream baseStream) {
            if (!baseStream.CanRead || !baseStream.CanSeek)
                throw new InvalidDataException("PAKArchive stream has to be readable and seekable");
            PAKArchive a = new PAKArchive(baseStream);
            BinaryReader reader = new BinaryReader(baseStream);
            if (reader.ReadUInt32() != 0)
                throw new InvalidDataException("Invalid PAKArchive magic (has to be 0x00000000)");
            UInt32 count = reader.ReadUInt32();
            while (count > 0) {
                count--;
                string name = reader.ReadZString().ToLower();
                if (name.StartsWith("..\\"))
                    name = name.Substring(3);
                FilePos pos;
                pos.offset = reader.ReadUInt32() + 4;
                pos.length = reader.ReadUInt32() - 8;
                a.files.Add(name, pos);
            }
            a.baseOffset = (UInt32)baseStream.Position;
            return a;
        }

        public byte[] readFile(string name) {
            name = name.ToLower();
            if (name.StartsWith("..\\"))
                name = name.Substring(3);
            if (!files.ContainsKey(name))
                throw new InvalidDataException("Archive does not contain file \"" + name + "\"");
            FilePos pos = files[name];
            byte[] buffer = new byte[pos.length];
            stream.Position = baseOffset + pos.offset;
            if (stream.Read(buffer, 0, (int)pos.length) != pos.length)
                throw new InvalidDataException("Could not read all file data of \"" + name + "\"");
            return buffer;
        }

        public string[] getDirContent(string name) {
            name = name.ToLower();
            if (name.StartsWith("..\\"))
                name = name.Substring(3);
            if (!name.EndsWith("\\"))
                name += "\\";
            List<string> r = new List<string>();
            foreach (string file in files.Keys) {
                if (file.StartsWith(name) && file.IndexOf('\\', name.Length) < 0)
                    r.Add(file.Substring(name.Length));
            }
            return r.ToArray();
        }
    }
}
