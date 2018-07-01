using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using zzio.utils;
using zzio.primitives;

namespace zzio.db
{
    public enum CellDataType
    {
        // To have a useful default ctor, this types are shifted by one
        Unknown = 0,

        String = 1,
        Integer = 2,
        // TODO: Figure out what 3 was
        ForeignKey = 4,
        Byte = 5,
        Buffer = 6,
    }

    [Serializable]
    public struct Cell
    {
        // This structure is a bit convoluted but allows Cell to be
        // a struct value type and it is not supposed to be extendable
        // make a better database file instead...

        private readonly string stringValue;
        private readonly int integerValue;
        private readonly byte byteValue;
        private readonly ForeignKey foreignKeyValue;
        private readonly byte[] bufferValue;

        private void checkType(CellDataType expected)
        {
            if (Type != expected)
                throw new InvalidOperationException("Cell data type is " + Type + " not " + expected);
        }

        public CellDataType Type { get; }
        public int ColumnIndex { get; }

        public string String { get { checkType(CellDataType.String); return stringValue; } }
        public int Integer { get { checkType(CellDataType.Integer); return integerValue; } }
        public byte Byte { get { checkType(CellDataType.Byte); return byteValue; } }
        public ForeignKey ForeignKey { get { checkType(CellDataType.ForeignKey); return foreignKeyValue; } }
        public byte[] Buffer { get { checkType(CellDataType.Buffer); return bufferValue.ToArray(); } }

        public Cell(string value, int columnIndex = -1) : this()
        {
            Type = CellDataType.String;
            stringValue = value;
            ColumnIndex = columnIndex;
        }

        public Cell(int value, int columnIndex = -1) : this()
        {
            Type = CellDataType.Integer;
            integerValue = value;
            ColumnIndex = columnIndex;
        }

        public Cell(byte value, int columnIndex = -1) : this()
        {
            Type = CellDataType.Byte;
            byteValue = value;
            ColumnIndex = columnIndex;
        }

        public Cell(ForeignKey value, int columnIndex = -1) : this()
        {
            Type = CellDataType.ForeignKey;
            foreignKeyValue = value;
            ColumnIndex = columnIndex;
        }

        public Cell(byte[] value, int columnIndex = -1) : this()
        {
            Type = CellDataType.Buffer;
            bufferValue = value.ToArray();
            ColumnIndex = columnIndex;
        }

        private static void readFixedSize(BinaryReader reader, UInt32 expectedSize)
        {
            UInt32 actualSize = reader.ReadUInt32();
            if (actualSize != expectedSize)
                throw new InvalidDataException("Invalid cell size: " + actualSize);
        }

        private static readonly Dictionary<CellDataType, Func<BinaryReader, dynamic>> dataReaders =
            new Dictionary<CellDataType, Func<BinaryReader, dynamic>>()
            {
                { CellDataType.String,     (r) => { return r.ReadZString(); } },
                { CellDataType.Integer,    (r) => { readFixedSize(r, 4); return r.ReadInt32(); } },
                { CellDataType.Byte,       (r) => { readFixedSize(r, 1); return r.ReadByte(); } },
                { CellDataType.ForeignKey, (r) => { readFixedSize(r, 8); return ForeignKey.ReadNew(r); } },
                { CellDataType.Buffer,     (r) => { return r.ReadBytes(r.ReadInt32()); } }
            };

        public Cell ReadNew(BinaryReader reader)
        {
            CellDataType type = EnumUtils.intToEnum<CellDataType>(reader.ReadInt32());
            int columnIndex = reader.ReadInt32();

            if (dataReaders.ContainsKey(type))
            {
                dynamic value = dataReaders[type](reader);
                return new Cell(value, columnIndex);
            }
            throw new InvalidDataException("Unknown cell data type");
        }

        private static readonly Dictionary<CellDataType, Action<BinaryWriter, Cell>> dataWriters =
            new Dictionary<CellDataType, Action<BinaryWriter, Cell>>()
            {
                { CellDataType.String,     (w, c) => { w.WriteZString(c.stringValue); } },
                { CellDataType.Integer,    (w, c) => { w.Write(4); w.Write(c.integerValue); } },
                { CellDataType.Byte,       (w, c) => { w.Write(1); w.Write(c.byteValue); } },
                { CellDataType.ForeignKey, (w, c) => { w.Write(8); c.foreignKeyValue.Write(w); } },
                { CellDataType.Buffer,     (w, c) => { w.Write(c.bufferValue.Length); w.Write(c.bufferValue); } }
            };

        public void Write(BinaryWriter writer)
        {
            writer.Write((int)Type);
            writer.Write(ColumnIndex);
            dataWriters[Type](writer, this);
        }
    }
}
