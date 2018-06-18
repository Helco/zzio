using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace zzio
{
    [System.Serializable]
    public struct VarConfigVar
    {
        public string name;
        public float floatValue;
        public string stringValue;
        public byte ignored;
    }

    [System.Serializable]
    public class VarConfig
    {
        public byte[] checksum; //always 16 bytes
        public UInt32 header; //3 bytes, meaning unknown
        public VarConfigVar firstValue; //name is always null
        public VarConfigVar[] vars;

        public VarConfig(byte[] chk, UInt32 h, VarConfigVar fV, VarConfigVar[] v)
        {
            checksum = chk;
            header = h;
            firstValue = fV;
            vars = v;
        }

        public static VarConfig read(byte[] buffer)
        {
            BinaryReader reader = new BinaryReader(new MemoryStream(buffer));
            byte[] checksum = reader.ReadBytes(16);
            UInt32 header = ((UInt32)reader.ReadUInt16() << 8) | reader.ReadByte();
            VarConfigVar firstValue = readVar(null, reader);
            List<VarConfigVar> vars = new List<VarConfigVar>();

            byte[] calculatedChecksum = MD5.Create().ComputeHash(buffer, 25, buffer.Length - 25);
            for (int i=0; i<16; i++)
            {
                if (calculatedChecksum[i] != checksum[i])
                    throw new Exception("Invalid checksum in configuration file");
            }

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var name = new StringBuilder(Utils.readSizedString(reader, reader.ReadByte()));
                for (int i = 0; i < name.Length; i++)
                    name[i] = (char)(name[i] ^ 0x75);
                if (reader.ReadByte() != 0)
                    throw new Exception("Configuration variable name does not end with \\0");
                vars.Add(readVar(name.ToString(), reader));
            }

            return new VarConfig(checksum, header, firstValue, vars.ToArray());
        }

        private static VarConfigVar readVar(string name, BinaryReader reader)
        {
            VarConfigVar v;
            v.name = name;
            v.floatValue = reader.ReadSingle();
            byte isString = reader.ReadByte();
            if (isString > 0)
                v.stringValue = Utils.readSizedString(reader, reader.ReadByte());
            else
                v.stringValue = null;
            v.ignored = reader.ReadByte();
            return v;
        }

        public byte[] write()
        {
            MemoryStream stream = new MemoryStream();
            write(stream);
            return stream.ToArray();
        }

        public void write(Stream stream)
        {
            if (!stream.CanSeek)
                //TODO: maybe write fallback with MemoryStream 
                throw new ArgumentException("Config.write needs a seekable stream");
            long startPosition = stream.Position;
            stream.Seek(16, SeekOrigin.Current);

            BinaryWriter rawWriter = new BinaryWriter(stream);
            rawWriter.Write(header);
            writeVar(rawWriter, firstValue);

            MD5 md5 = MD5.Create();
            CryptoStream hashStream = new CryptoStream(stream, md5, CryptoStreamMode.Write);
            BinaryWriter hashWriter = new BinaryWriter(hashStream);
            for (int i=0; i<vars.Length; i++)
            {
                if (vars[i].name.Length > 255)
                    throw new InvalidDataException("Config variable name is too long (>255)");
                if (vars[i].stringValue != null && vars[i].stringValue.Length > 255)
                    throw new InvalidDataException("Config string value is too long (>255)");

                byte[] nameBytes = Encoding.Default.GetBytes(vars[i].name);
                hashWriter.Write((byte)nameBytes.Length);
                for (int j=0; j<nameBytes.Length; i++)
                {
                    if (nameBytes[j] == 0)
                        throw new InvalidDataException("Config string value has invalid character ('\0')");
                    hashWriter.Write((char)(nameBytes[j] ^ 0x75));
                }
                hashWriter.Write((byte)0);

                writeVar(hashWriter, vars[i]);
            }
            hashStream.FlushFinalBlock();

            stream.Seek(startPosition, SeekOrigin.Begin);
            rawWriter.Write(md5.Hash, 0, 16);
        }

        private static void writeVar(BinaryWriter writer, VarConfigVar var)
        {
            writer.Write(var.floatValue);
            if (var.stringValue != null)
            {
                byte[] valueBytes = Encoding.Default.GetBytes(var.stringValue);
                writer.Write(valueBytes, 0, valueBytes.Length);
            }
            writer.Write((byte)0);
        }
    }
}
