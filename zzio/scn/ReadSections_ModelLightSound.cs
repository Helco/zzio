using System;
using System.IO;
using Newtonsoft.Json;

namespace zzio {
    namespace scn {
        [System.Serializable]
        public enum LightType {
            Directional = 1,
            Ambient = 2,
            Point = 128,
            Spot = 129,

            Unknown = -1
        }

        [System.Serializable]
        [Flags]
        public enum LightFlags
        {
            LightAtomics = 1,
            LightWorld = 2
        }

        [System.Serializable]
        public enum RenderType {
            //now with names from actual debug strings extracted from the main executable
            Standard1 = 0,
            Additive1 = 1,
            EnvMap32 = 2,
            EnvMap64 = 3,
            EnvMap96 = 4, //Cathedral
            EnvMap128 = 5,
            EnvMap196 = 6, //London windows
            EnvMap255 = 7, //London cupboard windows
            Standard2 = 8, //Metallic?
            Standard3 = 9, //Plants?
            Additive2 = 10, //Cob webs 
            Additive3 = 11,

            Unknown = -1
        }

        [System.Serializable]
        public struct Light {
            public UInt32 idx;
            public LightType type;
            public FColor color;
            public LightFlags flags;
            public Vector pos, vec; //vec is either dir or a lookAt point
            public float radius;
        }

        [System.Serializable]
        public struct FOModel {
            public UInt32 idx;
            public string filename;
            public Vector pos, rot;
            public float f1, f2, f3, f4, f5;
            [JsonConverter(typeof(JsonHexNumberConverter))]
            public UInt32 color;
            public byte worldDetailLevel, ff2;
            public RenderType renderType;
            public byte ff3;
            public Int32 i7;
        }

        [System.Serializable]
        public struct Model {
            public UInt32 idx;
            public string filename;
            public Vector pos, rot, scale;
            [JsonConverter(typeof(JsonHexNumberConverter))]
            public UInt32 color;
            public byte i1;
            public Int32 i15;
            public byte i2;
        }

        [System.Serializable]
        public struct DynModelData {
            public float a1, a2, a3, a4, a5, a6, a7;
            public byte someFlag;
            [JsonConverter(typeof(JsonHexNumberConverter))]
            public UInt32 someColor;
            public UInt32 cc;
            public string s1, s2;
        }
        [System.Serializable]
        public struct DynModel {
            public UInt32 idx, c1, c2;
            public Vector pos, rot;
            public float f1, f2;
            public Vector v1;
            public UInt32 ii1, ii2;
            public DynModelData[] data; //always three
        }

        [System.Serializable]
        public struct Sample3D {
            public UInt32 idx;
            public string filename;
            public Vector v1, v2, v3;
            public UInt32 i1, i2, i3, i4, i5;
        }

        [System.Serializable]
        public struct Sample2D {
            public UInt32 idx;
            public string filename;
            public UInt32 i1, i2;
            public byte c;
        }

        public partial class Scene {
            private static Light readLight(BinaryReader reader) {
                Light l;
                l.idx = reader.ReadUInt32();
                l.type = Utils.intToEnum<LightType>(reader.ReadInt32());
                l.color = FColor.read(reader);
                l.flags = (LightFlags)reader.ReadUInt32();
                l.pos = new Vector();
                l.vec = new Vector();
                l.radius = 0.0f;
                switch(l.type) {
                    case (LightType.Directional): {
                            l.pos = Vector.read(reader);
                            l.vec = Vector.read(reader);
                        }break;
                    case (LightType.Point): {
                            l.radius = reader.ReadSingle();
                            l.pos = Vector.read(reader);
                        }break;
                    case (LightType.Spot): {
                            l.radius = reader.ReadUInt32();
                            l.pos = Vector.read(reader);
                            l.vec = Vector.read(reader);
                        }break;
                }
                return l;
            }

            private static FOModel readFOModel(BinaryReader reader) {
                FOModel m;
                m.idx = reader.ReadUInt32();
                m.filename = Utils.readZString(reader);
                m.pos = Vector.read(reader);
                m.rot = Vector.read(reader);
                m.f1 = reader.ReadSingle();
                m.f2 = reader.ReadSingle();
                m.f3 = reader.ReadSingle();
                m.f4 = reader.ReadSingle();
                m.f5 = reader.ReadSingle();
                m.color = reader.ReadUInt32();
                m.worldDetailLevel = reader.ReadByte();
                m.ff2 = reader.ReadByte();
                m.renderType = Utils.intToEnum<RenderType>(reader.ReadInt32());
                m.ff3 = reader.ReadByte();
                m.i7 = reader.ReadInt32();
                return m;
            }

            private static Model readModel(BinaryReader reader) {
                Model m;
                m.idx = reader.ReadUInt32();
                m.filename = Utils.readZString(reader);
                m.pos = Vector.read(reader);
                m.rot = Vector.read(reader);
                m.scale = Vector.read(reader);
                m.color = reader.ReadUInt32();
                m.i1 = reader.ReadByte();
                m.i15 = reader.ReadInt32();
                m.i2 = reader.ReadByte();
                return m;
            }

            private static DynModel readDynModel(BinaryReader reader) {
                DynModel m;
                m.idx = reader.ReadUInt32();
                m.c1 = reader.ReadUInt32();
                m.c2 = reader.ReadUInt32();
                m.pos = Vector.read(reader);
                m.rot = Vector.read(reader);
                m.f1 = reader.ReadSingle();
                m.f2 = reader.ReadSingle();
                m.v1 = Vector.read(reader);
                m.ii1 = reader.ReadUInt32();
                m.ii2 = reader.ReadUInt32();
                m.data = new DynModelData[3];
                for (UInt32 i=0; i<3; i++) {
                    m.data[i].a1 = reader.ReadSingle();
                    m.data[i].a2 = reader.ReadSingle();
                    m.data[i].a3 = reader.ReadSingle();
                    m.data[i].a4 = reader.ReadSingle();
                    m.data[i].a5 = reader.ReadSingle();
                    m.data[i].a6 = reader.ReadSingle();
                    m.data[i].a7 = reader.ReadSingle();
                    m.data[i].someFlag = reader.ReadByte();
                    m.data[i].someColor = reader.ReadUInt32();
                    m.data[i].cc = reader.ReadUInt32();
                    m.data[i].s1 = Utils.readZString(reader);
                    m.data[i].s2 = Utils.readZString(reader);
                }
                return m;
            }

            private static Sample3D readSample3D(BinaryReader reader) {
                Sample3D s;
                s.idx = reader.ReadUInt32();
                s.filename = Utils.readZString(reader);
                s.v1 = Vector.read(reader);
                s.v2 = Vector.read(reader);
                s.v3 = Vector.read(reader);
                s.i1 = reader.ReadUInt32();
                s.i2 = reader.ReadUInt32();
                s.i3 = reader.ReadUInt32();
                s.i4 = reader.ReadUInt32();
                s.i5 = reader.ReadUInt32();
                return s;
            }

            private static Sample2D readSample2D(BinaryReader reader) {
                Sample2D s;
                s.idx = reader.ReadUInt32();
                s.filename = Utils.readZString(reader);
                s.i1 = reader.ReadUInt32();
                s.i2 = reader.ReadUInt32();
                s.c = reader.ReadByte();
                return s;
            }
        }
    }
}