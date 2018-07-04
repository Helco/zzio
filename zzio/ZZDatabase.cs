using System;
using System.IO;
using System.Collections.Generic;
using zzio.utils;

namespace zzio {
    public enum ZZDBDataType {
        String = 0,
        UInt = 1,
        UUID = 3,
        Byte = 4,
        Buffer = 5,

        Unknown = -1
    }

    public struct ZZDBUUID {
        public UInt32 uid, type;

        public override string ToString() { return type.ToString("X") + "|" + uid.ToString("X"); }
    }

    public struct ZZDBRowData
    {
        public uint columnIndex;
        public ZZDBDataType entryType;
        public object data;
    }

    public struct ZZDBRow {
        public ZZDBRowData[] columns;
        public UInt32 uid;
    }

    public class ZZDatabaseIndex {
        public UInt32[] columnNumbers; //no usage for them?
        public string[] columnNames;
        public UInt32 columnCount { get { return (UInt32)columnNames.Length; } }

        public ZZDatabaseIndex(UInt32[] cnum, string[] cnam) {
            columnNumbers = cnum;
            columnNames = cnam;
        }

        public static ZZDatabaseIndex read(byte[] buf) {
            BinaryReader reader = new BinaryReader(new MemoryStream(buf, false));
            UInt32 columnCount = reader.ReadUInt32();
            UInt32[] columnNumbers = new UInt32[columnCount];
            string[] columnNames = new string[columnCount];
            for (UInt32 i=0; i<columnCount; i++) {
                columnNumbers[i] = reader.ReadUInt32();
                columnNames[i] = reader.ReadZString();
            }
            return new ZZDatabaseIndex(columnNumbers, columnNames);
        }

        public void write(Stream s)
        {
            BinaryWriter writer = new BinaryWriter(s);
            writer.Write(columnNumbers.Length);
            for (int i = 0; i<columnNumbers.Length; i++)
            {
                writer.Write(columnNumbers[i]);
                writer.WriteZString(columnNames[i]);
            }
        }

        public byte[] write()
        {
            MemoryStream mem = new MemoryStream(512);
            write(mem);
            return mem.ToArray();
        }
    }

    public class ZZDatabase {
        public ZZDBRow[] rows;

        public ZZDatabase(ZZDBRow[] r) {
            rows = r;
        }

        public ZZDBRow getRowByUID (UInt32 uid)
        {
            foreach(ZZDBRow row in rows)
            {
                if (row.uid == uid)
                    return row;
            }
            ZZDBRow result;
            result.columns = null;
            result.uid = 0;
            return result;
        }

        public static ZZDatabase read(byte[] buf) {
            BinaryReader reader = new BinaryReader(new MemoryStream(buf, false));
            UInt32 rowCount = reader.ReadUInt32();
            ZZDBRow[] rows = new ZZDBRow[rowCount];
            for (UInt32 i = 0; i < rowCount; i++) {
                rows[i].uid = reader.ReadUInt32();
                UInt32 filledColumnCount = reader.ReadUInt32();
                rows[i].columns = new ZZDBRowData[filledColumnCount];
                for (UInt32 j = 0; j < filledColumnCount; j++) {
                    rows[i].columns[j].entryType = EnumUtils.intToEnum<ZZDBDataType>(reader.ReadInt32());
                    rows[i].columns[j].columnIndex = reader.ReadUInt32();
                    switch (rows[i].columns[j].entryType) {
                        case (ZZDBDataType.String): { rows[i].columns[j].data = reader.ReadZString(); } break;
                        case (ZZDBDataType.UInt): { reader.ReadUInt32(); rows[i].columns[j].data = reader.ReadUInt32(); } break;
                        case (ZZDBDataType.Byte): { reader.ReadUInt32(); rows[i].columns[j].data = reader.ReadByte(); } break;
                        case (ZZDBDataType.UUID): {
                                reader.ReadUInt32(); //redundant size 
                                ZZDBUUID uuid;
                                uuid.uid = reader.ReadUInt32();
                                uuid.type = reader.ReadUInt32();
                                rows[i].columns[j].data = uuid;
                            }
                            break;
                        default: {
                                UInt32 len = reader.ReadUInt32();
                                byte[] entryBuf = reader.ReadBytes((int)len);
                                rows[i].columns[j].data = entryBuf;
                            }
                            break;
                    }
                }
            }

            return new ZZDatabase(rows);
        }

        public void write(Stream s)
        {
            BinaryWriter writer = new BinaryWriter(s);
            writer.Write(rows.Length);
            for (int i=0; i<rows.Length; i++)
            {
                writer.Write(rows[i].uid);
                writer.Write(rows[i].columns.Length);
                for (int j=0; j<rows[i].columns.Length; i++)
                {
                    ZZDBRowData data = rows[i].columns[j];
                    writer.Write((int)data.entryType);
                    writer.Write(data.columnIndex);
                    switch(data.entryType)
                    {
                        case (ZZDBDataType.String): { writer.WriteZString(data.data as String); }break;
                        case (ZZDBDataType.UInt): { writer.Write(4); writer.Write((uint)data.data); }break;
                        case (ZZDBDataType.Byte): { writer.Write(1); writer.Write((byte)data.data); }break;
                        case (ZZDBDataType.UUID):
                            {
                                writer.Write(8);
                                ZZDBUUID uuid = (ZZDBUUID)data.data;
                                writer.Write(uuid.uid);
                                writer.Write(uuid.type);
                            }break;
                        case (ZZDBDataType.Buffer):
                            {
                                byte[] buf = data.data as byte[];
                                writer.Write(buf.Length);
                                writer.Write(buf, 0, buf.Length);
                            }break;
                    }
                }
            }
        }

        public byte[] write()
        {
            MemoryStream mem = new MemoryStream(8192);
            write(mem);
            return mem.ToArray();
        }
    }
}
