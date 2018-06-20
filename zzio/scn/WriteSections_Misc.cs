using System;
using System.IO;
using zzio.utils;

namespace zzio
{
    namespace scn
    {
        public partial class Scene
        {
            private static void writeVersion(BinaryWriter writer, Version version)
            {
                writer.WriteZString(version.author);
                writer.Write((int)version.country);
                writer.Write((int)version.type);
                writer.Write(version.v3);
                writer.Write(version.buildVersion);
                writer.WriteZString(version.date);
                writer.WriteZString(version.time);
                writer.Write(version.year);
                writer.Write(version.vv2);
            }

            private static void writeMisc(BinaryWriter writer, Misc m)
            {
                writer.WriteZString(m.worldFile);
                writer.WriteZString(m.worldPath);
                writer.WriteZString(m.texturePath);
                m.ambientLight.write(writer);
                m.v1.write(writer);
                m.v2.write(writer);
                m.clearColor.write(writer);
                writer.Write(m.fogType);
                if (m.fogType != 0)
                {
                    m.fogColor.write(writer);
                    writer.Write(m.fogDistance);
                }
                writer.Write(m.f1);
                writer.Write(m.farClip);
            }

            private static void writeWaypointSystem(BinaryWriter writer, WaypointSystem s)
            {
                writer.Write(s.version);
                writer.Write(s.mustBeZero);
                if (s.version >= 5)
                    writer.Write(s.data, 0, 0x18);
                WaypointData[] d = s.waypointData;
                writer.Write(d.Length);
                for (int i=0; i<d.Length; i++)
                {
                    writer.Write(d[i].ii1);
                    if (s.version >= 4)
                        writer.Write(d[i].ii1ext);
                    d[i].v1.write(writer);

                    writer.Write(d[i].innerdata1.Length);
                    for (int j = 0; j < d[i].innerdata1.Length; j++)
                        writer.Write(d[i].innerdata1[j]);

                    writer.Write(d[i].innerdata2.Length);
                    for (int j = 0; j < d[i].innerdata2.Length; j++)
                        writer.Write(d[i].innerdata2[j]);
                }

                if (s.version >= 2)
                {
                    WaypointInnerData[] d2 = s.inner2data1;
                    writer.Write(d2.Length);
                    for (int i=0; i<d2.Length; i++)
                    {
                        writer.Write(d2[i].iiv2);
                        writer.Write(d2[i].data.Length);
                        for (int j = 0; j < d2[i].data.Length; j++)
                            writer.Write(d2[i].data[j]);
                    }
                }

                if (s.version >= 3)
                {
                    for (int i=0; i<d.Length; i++)
                    {
                        writer.Write(d[i].inner3data1.Length);
                        for (int j = 0; j < d[i].inner3data1.Length; j++)
                            writer.Write(d[i].inner3data1[j]);
                    }
                }

                writer.Write(s.mustBeFFFF);
            }

            private static void writeDataset(BinaryWriter writer, Dataset s)
            {
                if (s.dataSize != 0x20 && s.dataSize != 0x24)
                    throw new InvalidDataException("Unknown size for dataset structure");
                writer.Write(s.dataSize);
                writer.Write(s.sceneId);
                writer.Write((int)s.sceneType);
                writer.Write(s.nameUID);
                writer.Write(s.unk1);
                writer.Write(s.padding1);
                writer.Write(s.unk2);
                writer.Write(s.isLondon);
                writer.Write(s.padding2);
                writer.Write(s.padding3);
                writer.Write(s.padding4);
                writer.Write(s.unk4);
                writer.Write(s.unk5);
                if (s.dataSize > 0x20)
                    writer.Write((uint)(s.unk6 ? 1 : 0));
                writer.WriteZString(s.s1);
                writer.WriteZString(s.s2);
            }

            private static void writeVertexModifier(BinaryWriter writer, VertexModifier m)
            {
                writer.Write(m.idx);
                writer.Write(m.type);
                m.v.write(writer);
                writer.Write(m.color);
                if (m.type == 1)
                    m.vv.write(writer);
                writer.Write(m.ii);
                writer.Write(m.c);
            }

            private static void writeTextureProperty(BinaryWriter writer, TextureProperty p)
            {
                writer.WriteZString(p.fileName);
                writer.Write(p.ii);
            }

            private static void writeSceneItem(BinaryWriter writer, SceneItem i)
            {
                writer.WriteZString(i.s);
                writer.Write(i.i1);
                writer.Write(i.i2);
            }
        }
    }
}
