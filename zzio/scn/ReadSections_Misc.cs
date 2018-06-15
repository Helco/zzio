using System;
using System.IO;
using Newtonsoft.Json;

namespace zzio {
    namespace scn {
        [System.Serializable]
        public enum VersionBuildType
        {
            Debug = 0,
            Release = 1,
            WebDemo = 2,
            CdDemo = 3,
            
            Unknown = -1
        }

        [System.Serializable]
        public enum VersionBuildCountry
        {
            Germany = 0,
            English = 1,
            France = 2,
            Spain = 3,
            Italy = 4,
            Japanese = 5,
            English6 = 6,
            Russia = 8,

            Unknown = -1
        }

        [System.Serializable]
        public enum SceneType
        {
            Overworld = 0,
            Arena = 1,
            MultiplayerArena = 2,

            Unknown = -1
        }

        [System.Serializable]
        public struct Version {
            public string author;
            public VersionBuildCountry country;
            public VersionBuildType type;
            public UInt32 v3, buildVersion;
            public string date, time;
            public UInt32 year, vv2;
        }

        [System.Serializable]
        public struct Misc {
            public string worldFile, worldPath, texturePath;
            public FColor ambientLight;
            public Vector v1, v2;
            public IColor clearColor;
            public byte fogType;
            public IColor fogColor;
            public float fogDistance;
            public float f1, farClip;
        }

        [System.Serializable]
        public struct WaypointInnerData {
            public UInt32 iiv2;
            public UInt32[] data;
        }
        [System.Serializable]
        public struct WaypointData {
            public UInt32 ii1, ii1ext, iiv2;
            public Vector v1;
            public UInt32[] innerdata1, innerdata2;
            public UInt32[] inner3data1;
        }
        [System.Serializable]
        public struct WaypointSystem {
            public UInt32 version, mustBeZero, mustBeFFFF;
            public byte[] data;
            public WaypointData[] waypointData;
            public WaypointInnerData[] inner2data1;
        }

        [System.Serializable]
        public struct Dataset {
            public uint dataSize;
            public bool hasUnk6 { get { return dataSize >= 0x20; } }
            public uint sceneId;
            public SceneType sceneType;
            public uint nameUID;
            public ushort unk1, padding1;
            public bool unk2, isLondon;
            public byte padding2, padding3, padding4;
            public uint unk4;
            public bool unk5, unk6; 
            public string s1, s2;
        }

        [System.Serializable]
        public struct VertexModifier {
            public UInt32 idx, type;
            public Vector v;
            [JsonConverter(typeof(JsonHexNumberConverter))]
            public UInt32 color;
            public Vector vv;
            public UInt32 ii;
            public byte c;
        }

        [System.Serializable]
        public struct TextureProperty {
            public string fileName;
            public Int32 ii;
        }

        [System.Serializable]
        public struct SceneItem {
            public string s;
            public UInt32 i1, i2;
        }
        
        public partial class Scene {
            private static Version readVersion (BinaryReader reader) {
                Version v;
                v.author = Utils.readZString(reader);
                v.country = Utils.intToEnum<VersionBuildCountry>(reader.ReadInt32());
                v.type = Utils.intToEnum<VersionBuildType>(reader.ReadInt32());
                v.v3 = reader.ReadUInt32();
                v.buildVersion = reader.ReadUInt32();
                v.date = Utils.readZString(reader);
                v.time = Utils.readZString(reader);
                v.year = reader.ReadUInt32();
                v.vv2 = reader.ReadUInt32();
                return v;
            }

            private static Misc readMisc (BinaryReader reader) {
                Misc m;
                m.worldFile = Utils.readZString(reader);
                m.worldPath = Utils.readZString(reader);
                m.texturePath = Utils.readZString(reader);
                m.ambientLight = FColor.read(reader);
                m.v1 = Vector.read(reader);
                m.v2 = Vector.read(reader);
                m.clearColor = IColor.read(reader);
                m.fogType = reader.ReadByte();
                if (m.fogType != 0) {
                    m.fogColor = IColor.read(reader);
                    m.fogDistance = reader.ReadSingle();
                }
                else
                {
                    m.fogColor = new IColor();
                    m.fogDistance = 0.0f;
                }
                m.f1 = reader.ReadSingle();
                m.farClip = reader.ReadSingle();
                return m;
            }

            private static WaypointSystem readWaypointSystem (BinaryReader reader) {
                //I am crying...
                WaypointSystem s;
                s.version = reader.ReadUInt32();
                s.mustBeZero = reader.ReadUInt32();
                s.data = null;
                s.waypointData = null;
                s.inner2data1 = null;
                s.mustBeFFFF = 0;
                if (s.mustBeZero == 0) {
                    if (s.version >= 5)
                        s.data = reader.ReadBytes(0x18);
                    UInt32 count1 = reader.ReadUInt32();
                    WaypointData[] d = new WaypointData[count1];
                    for (UInt32 i=0; i<count1; i++) {
                        d[i].ii1 = reader.ReadUInt32();
                        if (s.version >= 4)
                            d[i].ii1ext = reader.ReadUInt32();
                        d[i].v1 = Vector.read(reader);

                        UInt32 ci1 = reader.ReadUInt32();
                        d[i].innerdata1 = new UInt32[ci1];
                        for (UInt32 j = 0; j < ci1; j++)
                            d[i].innerdata1[j] = reader.ReadUInt32();

                        UInt32 ci2 = reader.ReadUInt32();
                        d[i].innerdata2 = new UInt32[ci2];
                        for (UInt32 j = 0; j < ci2; j++)
                            d[i].innerdata2[j] = reader.ReadUInt32();
                    }

                    if (s.version >= 2) {
                        UInt32 count2 = reader.ReadUInt32();
                        s.inner2data1 = new WaypointInnerData[count2];
                        for (UInt32 j = 0; j < count2; j++) {
                            s.inner2data1[j].iiv2 = reader.ReadUInt32();
                            UInt32 ci3 = reader.ReadUInt32();
                            s.inner2data1[j].data = new UInt32[ci3];
                            for (UInt32 k = 0; k < ci3; k++)
                                s.inner2data1[j].data[k] = reader.ReadUInt32();
                        }
                    }

                    if (s.version >= 3) {
                        for (UInt32 j = 0; j < count1; j++) {
                            UInt32 ci4 = reader.ReadUInt32();
                            d[j].inner3data1 = new UInt32[ci4];
                            for (UInt32 k = 0; k < ci4; k++)
                                d[j].inner3data1[k] = reader.ReadUInt32();
                        }
                    }
                    s.waypointData = d;

                    s.mustBeFFFF = reader.ReadUInt32();
                }
                return s;
            }

            private static Dataset readDataset (BinaryReader reader) {
                Dataset d;
                d.dataSize = reader.ReadUInt32();
                if (d.dataSize != 0x20 && d.dataSize != 0x24)
                    throw new InvalidDataException("Unknown size for dataset structure");
                d.sceneId = reader.ReadUInt32();
                d.sceneType = Utils.intToEnum<SceneType>(reader.ReadInt32());
                d.nameUID = reader.ReadUInt32();
                d.unk1 = reader.ReadUInt16();
                d.padding1 = reader.ReadUInt16();
                d.unk2 = reader.ReadUInt32() != 0;
                d.isLondon = reader.ReadByte() != 0;
                d.padding2 = reader.ReadByte();
                d.padding3 = reader.ReadByte();
                d.padding4 = reader.ReadByte();
                d.unk4 = reader.ReadUInt32();
                d.unk5 = reader.ReadUInt32() != 0;
                if (d.dataSize > 0x20)
                    d.unk6 = reader.ReadUInt32() != 0;
                else
                    d.unk6 = false;
                d.s1 = Utils.readZString(reader);
                d.s2 = Utils.readZString(reader);
                return d;
            }

            private static VertexModifier readVertexModifier (BinaryReader reader) {
                VertexModifier vm;
                vm.idx = reader.ReadUInt32();
                vm.type = reader.ReadUInt32();
                vm.v = Vector.read(reader);
                vm.color = reader.ReadUInt32();
                if (vm.type == 1)
                    vm.vv = Vector.read(reader);
                else
                    vm.vv = new Vector();
                vm.ii = reader.ReadUInt32();
                vm.c = reader.ReadByte();
                return vm;
            }

            private static TextureProperty readTexProperty(BinaryReader reader) {
                TextureProperty p;
                p.fileName = Utils.readZString(reader);
                p.ii = reader.ReadInt32();
                return p;
            }

            private static SceneItem readSceneItem(BinaryReader reader) {
                SceneItem i;
                i.s = Utils.readZString(reader);
                i.i1 = reader.ReadUInt32();
                i.i2 = reader.ReadUInt32();
                return i;
            }
        }
    }
}