using System;
using System.IO;

namespace zzio.scn
{
    public partial class Scene
    {
        private static void writeLight(BinaryWriter writer, Light l)
        {
            writer.Write(l.idx);
            writer.Write((int)l.type);
            l.color.write(writer);
            writer.Write((uint)l.flags);
            switch(l.type)
            {
                case (LightType.Directional): {
                        l.pos.write(writer);
                        l.vec.write(writer);
                    } break;
                case (LightType.Point):
                    {
                        writer.Write(l.radius);
                        l.pos.write(writer);
                    }break;
                case (LightType.Spot):
                    {
                        writer.Write(l.radius);
                        l.pos.write(writer);
                        l.vec.write(writer);
                    }break;
            }
        }

        private static void writeFOModel(BinaryWriter writer, FOModel m)
        {
            writer.Write(m.idx);
            Utils.writeZString(writer, m.filename);
            m.pos.write(writer);
            m.rot.write(writer);
            writer.Write(m.f1);
            writer.Write(m.f2);
            writer.Write(m.f3);
            writer.Write(m.f4);
            writer.Write(m.f5);
            writer.Write(m.color);
            writer.Write(m.worldDetailLevel);
            writer.Write(m.ff2);
            writer.Write((int)m.renderType);
            writer.Write(m.ff3);
            writer.Write(m.i7);
        }

        private static void writeModel(BinaryWriter writer, Model m)
        {
            writer.Write(m.idx);
            Utils.writeZString(writer, m.filename);
            m.pos.write(writer);
            m.rot.write(writer);
            m.scale.write(writer);
            writer.Write(m.color);
            writer.Write(m.i1);
            writer.Write(m.i15);
            writer.Write(m.i2);
        }

        private static void writeDynModel(BinaryWriter writer, DynModel m)
        {
            writer.Write(m.idx);
            writer.Write(m.c1);
            writer.Write(m.c2);
            m.pos.write(writer);
            m.rot.write(writer);
            writer.Write(m.f1);
            writer.Write(m.f2);
            m.v1.write(writer);
            writer.Write(m.ii1);
            writer.Write(m.ii2);
            for (int i=0; i<3; i++)
            {
                writer.Write(m.data[i].a1);
                writer.Write(m.data[i].a2);
                writer.Write(m.data[i].a3);
                writer.Write(m.data[i].a4);
                writer.Write(m.data[i].a5);
                writer.Write(m.data[i].a6);
                writer.Write(m.data[i].a7);
                writer.Write(m.data[i].someFlag);
                writer.Write(m.data[i].someColor);
                writer.Write(m.data[i].cc);
                Utils.writeZString(writer, m.data[i].s1);
                Utils.writeZString(writer, m.data[i].s2);
            }
        }

        private static void writeSample3D(BinaryWriter writer, Sample3D s)
        {
            writer.Write(s.idx);
            Utils.writeZString(writer, s.filename);
            s.v1.write(writer);
            s.v2.write(writer);
            s.v3.write(writer);
            writer.Write(s.i1);
            writer.Write(s.i2);
            writer.Write(s.i3);
            writer.Write(s.i4);
            writer.Write(s.i5);
        }

        private static void writeSample2D(BinaryWriter writer, Sample2D s)
        {
            writer.Write(s.idx);
            Utils.writeZString(writer, s.filename);
            writer.Write(s.i1);
            writer.Write(s.i2);
            writer.Write(s.c);
        }
    }
}