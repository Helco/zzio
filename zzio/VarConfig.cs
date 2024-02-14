using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace zzio;

public struct VarConfigValue
{
    public readonly float floatValue;
    public readonly string stringValue;

    private VarConfigValue(float floatValue, string stringValue)
    {
        this.floatValue = floatValue;
        this.stringValue = stringValue;
    }

    public static VarConfigValue ReadNew(BinaryReader reader)
    {
        float floatValue = reader.ReadSingle();
        byte isString = reader.ReadByte();
        string stringValue = isString == 0 ? ""
            : VarConfig.ReadEncryptedString(reader);
        reader.ReadByte(); // ignored byte at the end
        return new VarConfigValue(floatValue, stringValue);
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(floatValue);
        writer.Write((byte)(stringValue.Length > 0 ? 1 : 0));
        if (stringValue.Length > 0)
            VarConfig.WriteEncryptedString(writer, stringValue);
        writer.Write((byte)1);
    }
}

[Serializable]
public class VarConfig
{
    private const byte XOR_KEY = 0x75;

    public byte[] header = new byte[3]; // 3 bytes, meaning unknown
    public VarConfigValue firstValue; // name is always empty
    public Dictionary<string, VarConfigValue> variables =
        [];

    public static VarConfig ReadNew(Stream stream)
    {
        VarConfig config = new();

        // Because of the Hash, two passes are necessary
        byte[] buffer = new byte[stream.Length - stream.Position];
        if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
            throw new InvalidDataException("Could not read VarConfig buffer");
        using BinaryReader reader = new(new MemoryStream(buffer, false));

        byte[] expectedChecksum = reader.ReadBytes(16);
        config.header = reader.ReadBytes(3);
        config.firstValue = VarConfigValue.ReadNew(reader);

        int startOfHashed = (int)(reader.BaseStream.Position);
        while (reader.PeekChar() >= 0)
        {
            string name = ReadEncryptedString(reader);
            VarConfigValue value = VarConfigValue.ReadNew(reader);
            config.variables[name] = value;
        }

#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
        var actualChecksum = MD5.HashData(buffer.AsSpan(startOfHashed..));
        if (!actualChecksum.SequenceEqual(expectedChecksum))
            throw new InvalidDataException("VarConfig checksums do not match");
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms

        return config;
    }

    public void Write(Stream stream)
    {
        if (!stream.CanSeek)
            // TODO: maybe write fallback with MemoryStream  
            throw new ArgumentException("Config.write needs a seekable stream");
        long startPosition = stream.Position;
        stream.Seek(16, SeekOrigin.Current);

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(header);
            firstValue.Write(writer);
        }

#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
        MD5 md5 = MD5.Create();
        CryptoStream hashStream = new(stream, md5, CryptoStreamMode.Write);
        using (var writer = new BinaryWriter(hashStream, Encoding.UTF8, leaveOpen: true))
            foreach (KeyValuePair<string, VarConfigValue> pair in variables)
            {
                WriteEncryptedString(writer, pair.Key);
                pair.Value.Write(writer);
            }
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms

        hashStream.FlushFinalBlock();
        stream.Seek(startPosition, SeekOrigin.Begin);
        stream.Write(md5.Hash!, 0, 16);
    }

    public static string ReadEncryptedString(BinaryReader reader)
    {
        byte stringLen = reader.ReadByte();
        byte[] buffer = reader.ReadBytes(stringLen)
            .Select(b => (byte)(b ^ XOR_KEY))
            .ToArray();
        reader.ReadByte(); // ignored terminator byte
        return Encoding.UTF8.GetString(buffer);
    }

    public static void WriteEncryptedString(BinaryWriter writer, string str)
    {
        if (str.Length > 255)
            throw new InvalidOperationException("String is too long for VarConfig");
        writer.Write((byte)str.Length);
        writer.Write(Encoding.UTF8.GetBytes(str)
            .Select(b => (byte)(b ^ XOR_KEY))
            .ToArray());
        writer.Write((byte)0);
    }
}
