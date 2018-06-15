using System;
using System.IO;

namespace zzio.scn
{
    public partial class Scene
    {
        private static void writeTrigger(BinaryWriter writer, Trigger t)
        {
            writer.Write(t.idx);
            writer.Write((int)t.colliderType);
            writer.Write(t.normalizeDir);
            t.dir.write(writer);
            writer.Write((int)t.type);
            writer.Write(t.ii1);
            writer.Write(t.ii2);
            writer.Write(t.ii3);
            writer.Write(t.ii4);
            Utils.writeZString(writer, t.s);
            t.pos.write(writer);
            switch(t.colliderType)
            {
                case (TriggerColliderType.Box):
                    {
                        t.size.write(writer);
                    }break;
                case (TriggerColliderType.Sphere):
                    {
                        writer.Write(t.radius);
                    }break;
            }
        }

        private static void writeEffect(BinaryWriter writer, Effect e)
        {
            writer.Write(e.idx);
            writer.Write((int)e.type);
            switch(e.type)
            {
                case (EffectType.Unknown1):
                case (EffectType.Unknown5):
                case (EffectType.Unknown6):
                case (EffectType.Unknown10):
                    {
                        writer.Write(e.param);
                        e.v1.write(writer);
                        e.v2.write(writer);
                    }break;
                case (EffectType.Unknown4):
                    {
                        writer.Write(e.param);
                        e.v1.write(writer);
                    }break;
                case (EffectType.Unknown7):
                    {
                        Utils.writeZString(writer, e.effectFile);
                        e.v1.write(writer);
                    }break;
                case (EffectType.Unknown13):
                    {
                        Utils.writeZString(writer, e.effectFile);
                        e.v1.write(writer);
                        e.v2.write(writer);
                        e.v3.write(writer);
                        writer.Write(e.param);
                    }break;
            }
        }

        private static void writeEffectV2(BinaryWriter writer, EffectV2 e)
        {
            writer.Write(e.idx);
            writer.Write((int)e.type);
            writer.Write(e.i1);
            writer.Write(e.i2);
            writer.Write(e.i3);
            writer.Write(e.i4);
            writer.Write(e.i5);
            switch(e.type)
            {
                case (EffectV2Type.Unknown1):
                case (EffectV2Type.Unknown6):
                case (EffectV2Type.Unknown10):
                    {
                        writer.Write(e.param);
                        e.v1.write(writer);
                        e.v2.write(writer);
                    }break;
                case (EffectV2Type.Snowflakes):
                    {
                        writer.Write(e.param);
                    }break;
                case (EffectV2Type.Unknown13):
                    {
                        Utils.writeZString(writer, e.s);
                        e.v1.write(writer);
                        e.v2.write(writer);
                        e.v3.write(writer);
                        writer.Write(e.param);
                    }break;
            }
        }

        private static void writeBehaviour(BinaryWriter writer, Behaviour b)
        {
            writer.Write((int)b.type);
            writer.Write(b.modelId);
        }
    }
}
