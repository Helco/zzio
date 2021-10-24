using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [Serializable]
    public enum SceneType
    {
        Overworld = 0,
        Arena = 1,
        MultiplayerArena = 2,

        Unknown = -1
    }

    [Serializable]
    public class Dataset : ISceneSection
    {
        public uint sceneId;
        public SceneType sceneType;
        public uint nameUID;
        public ushort unk1;
        public bool isInterior, isLondon;
        public uint unk4;
        public bool isHotScene, unk6;
        public string s1 = "", s2 = "";

        public void Read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            UInt32 dataSize = reader.ReadUInt32();
            if (dataSize != 0x20 && dataSize != 0x24)
                throw new InvalidDataException("Unknown size for dataset structure");
            sceneId = reader.ReadUInt32();
            sceneType = EnumUtils.intToEnum<SceneType>(reader.ReadInt32());
            nameUID = reader.ReadUInt32();
            unk1 = reader.ReadUInt16();
            reader.ReadUInt16(); // padding
            isInterior = reader.ReadUInt32() != 0;
            isLondon = reader.ReadByte() != 0;
            reader.ReadBytes(3); // padding
            unk4 = reader.ReadUInt32();
            isHotScene = reader.ReadUInt32() != 0;
            if (dataSize > 0x20)
                unk6 = reader.ReadUInt32() != 0;
            else
                unk6 = false;
            s1 = reader.ReadZString();
            s2 = reader.ReadZString();
        }

        public void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(0x24);
            writer.Write(sceneId);
            writer.Write((int)sceneType);
            writer.Write(nameUID);
            writer.Write(unk1);
            writer.Write((UInt16)0); // padding
            writer.Write((uint)(isInterior ? 1 : 0));
            writer.Write(isLondon);
            writer.Write((UInt16)0); // padding
            writer.Write((byte)0);
            writer.Write(unk4);
            writer.Write((uint)(isHotScene ? 1 : 0));
            writer.Write((uint)(unk6 ? 1 : 0));
            writer.WriteZString(s1);
            writer.WriteZString(s2);
        }
    }
}
