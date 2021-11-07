using System;
using System.IO;
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
    public struct Cell : IEquatable<Cell>
    {
        // This structure is a bit tricky but allows Cell to be
        // a struct value type and it is not supposed to be extendable
        // make a better database file instead...

        private readonly string? stringValue;
        private readonly int integerValue;
        private readonly byte byteValue;
        private readonly ForeignKey foreignKeyValue;
        private readonly byte[]? bufferValue;

        private void checkType(CellDataType expected)
        {
            if (Type != expected)
                throw new InvalidOperationException("Cell data type is " + Type + " not " + expected);
        }

        public CellDataType Type { get; }
        public int ColumnIndex { get; }

        public string String         { get { checkType(CellDataType.String); return stringValue!; } }
        public int Integer           { get { checkType(CellDataType.Integer); return integerValue; } }
        public byte Byte             { get { checkType(CellDataType.Byte); return byteValue; } }
        public ForeignKey ForeignKey { get { checkType(CellDataType.ForeignKey); return foreignKeyValue; } }
        public byte[] Buffer         { get { checkType(CellDataType.Buffer); return bufferValue!.ToArray(); } }

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

        public override bool Equals(object? obj) => obj is Cell cell && Equals(cell);

        public bool Equals(Cell cell)
        {
            if (Type != cell.Type || ColumnIndex != cell.ColumnIndex)
                return false;
            switch(Type)
            {
                case CellDataType.String:     return stringValue == cell.stringValue;
                case CellDataType.Integer:    return integerValue == cell.integerValue;
                case CellDataType.Byte:       return byteValue == cell.byteValue;
                case CellDataType.ForeignKey: return foreignKeyValue.Equals(cell.foreignKeyValue);
                case CellDataType.Buffer:     return bufferValue!.SequenceEqual(cell.bufferValue!);
                default: return false;
            }
        }

        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
        
        public override int GetHashCode()
        {
            int hashCode = HashCode.Combine(Type, ColumnIndex);
            switch(Type)
            {
                case CellDataType.String:     hashCode ^= stringValue!.GetHashCode(); break;
                case CellDataType.Integer:    hashCode ^= integerValue; break;
                case CellDataType.Byte:       hashCode ^= byteValue; break;
                case CellDataType.ForeignKey: hashCode ^= foreignKeyValue.GetHashCode(); break;
                case CellDataType.Buffer:     hashCode ^= bufferValue!.GetHashCode(); break;
                default: break;
            }
            return hashCode;
        }

        private static void readFixedSize(BinaryReader reader, UInt32 expectedSize)
        {
            uint actualSize = reader.ReadUInt32();
            if (actualSize != expectedSize)
                throw new InvalidDataException("Invalid cell size: " + actualSize);
        }
        
        public static Cell ReadNew(BinaryReader reader)
        {
            CellDataType type = EnumUtils.intToEnum<CellDataType>(reader.ReadInt32() + 1);
            int columnIndex = reader.ReadInt32();

            // We could do better with dynamic, but Unity can't
            switch(type)
            {
                case CellDataType.String:
                {
                    string value = reader.ReadZString();
                    return new Cell(value, columnIndex);
                }
                case CellDataType.Integer:
                {
                    readFixedSize(reader, 4);
                    int value = reader.ReadInt32();
                    return new Cell(value, columnIndex);
                }
                case CellDataType.Byte:
                {
                    readFixedSize(reader, 1);
                    byte value = reader.ReadByte();
                    return new Cell(value, columnIndex);
                }
                case CellDataType.ForeignKey:
                {
                    readFixedSize(reader, 8);
                    ForeignKey value = ForeignKey.ReadNew(reader);
                    return new Cell(value, columnIndex);
                }
                case CellDataType.Buffer:
                {
                    int length = reader.ReadInt32();
                    byte[] value = reader.ReadBytes(length);
                    return new Cell(value, columnIndex);
                }
                default:
                    throw new InvalidDataException("Unknown cell data type");
            }
        }

        private static readonly Dictionary<CellDataType, Action<BinaryWriter, Cell>> dataWriters =
            new Dictionary<CellDataType, Action<BinaryWriter, Cell>>()
            {
                { CellDataType.String,     (w, c) => { w.WriteTZString(c.stringValue!); } },
                { CellDataType.Integer,    (w, c) => { w.Write(4); w.Write(c.integerValue); } },
                { CellDataType.Byte,       (w, c) => { w.Write(1); w.Write(c.byteValue); } },
                { CellDataType.ForeignKey, (w, c) => { w.Write(8); c.foreignKeyValue.Write(w); } },
                { CellDataType.Buffer,     (w, c) => { w.Write(c.bufferValue!.Length); w.Write(c.bufferValue!); } }
            };

        public void Write(BinaryWriter writer)
        {
            writer.Write((int)Type - 1);
            writer.Write(ColumnIndex);
            dataWriters[Type](writer, this);
        }
    }
}
