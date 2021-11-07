using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using zzio.primitives;
using zzio.utils;

namespace zzio
{
    namespace scn
    {
        [Serializable]
        public partial class Scene
        {
            public Version version = new Version();
            public Misc misc = new Misc();
            public WaypointSystem waypointSystem = new WaypointSystem();
            public Dataset dataset = new Dataset();
            public Vector sceneOrigin;
            public string backdropFile = "";
            public Light[] lights                       = new Light[0];
            public Model[] models                       = new Model[0];
            public FOModel[] foModels                   = new FOModel[0];
            public DynModel[] dynModels                 = new DynModel[0];
            public Trigger[] triggers                   = new Trigger[0];
            public Sample2D[] samples2D                 = new Sample2D[0];
            public Sample3D[] samples3D                 = new Sample3D[0];
            public Effect[] effects                     = new Effect[0];
            public EffectV2[] effects2                  = new EffectV2[0];
            public VertexModifier[] vertexModifiers     = new VertexModifier[0];
            public TextureProperty[] textureProperties  = new TextureProperty[0];
            public Behavior[] behaviors                 = new Behavior[0];
            public SceneItem[] sceneItems               = new SceneItem[0];
            public UInt32 ambientSound;
            public UInt32 music;

            private static T[] readArray<T>(BinaryReader reader, Func<T> ctor) where T : ISceneSection
            {
                UInt32 count = reader.ReadUInt32();
                T[] result = new T[count];
                for (UInt32 i = 0; i < count; i++)
                    (result[i] = ctor()).Read(new GatekeeperStream(reader.BaseStream));
                return result;
            }

            public void Read(Stream stream)
            {
                bool shouldReadNext = true;
                using BinaryReader reader = new BinaryReader(stream);
                if (reader.ReadZString() != "[Scenefile]")
                    throw new InvalidDataException("Magic section name missing in scene file");

                Dictionary<string, Action> sectionHandlers = new Dictionary<string, Action>()
                {
                    { "[Version]",           () => (version =          new Version()).Read(new GatekeeperStream(stream)) },
                    { "[Misc]",              () => (misc =             new Misc()).Read(new GatekeeperStream(stream)) },
                    { "[WaypointSystem]",    () => (waypointSystem =   new WaypointSystem()).Read(new GatekeeperStream(stream)) },
                    { "[Dataset]",           () => (dataset =          new Dataset()).Read(new GatekeeperStream(stream)) },
                    { "[SceneOrigin]",       () => sceneOrigin =       Vector.ReadNew(reader) },
                    { "[Backdrop]",          () => backdropFile =      reader.ReadZString() },
                    { "[AmbientSound]",      () => ambientSound =      reader.ReadUInt32() },
                    { "[Music]",             () => music =             reader.ReadUInt32() },

                    { "[Lights]",            () => lights =            readArray(reader, () => new Light()) },
                    { "[FOModels_v4]",       () => foModels =          readArray(reader, () => new FOModel()) },
                    { "[Models_v3]",         () => models =            readArray(reader, () => new Model()) },
                    { "[DynamicModels]",     () => dynModels =         readArray(reader, () => new DynModel()) },
                    { "[Triggers]",          () => triggers =          readArray(reader, () => new Trigger()) },
                    { "[2DSamples_v2]",      () => samples2D =         readArray(reader, () => new Sample2D()) },
                    { "[3DSamples_v2]",      () => samples3D =         readArray(reader, () => new Sample3D()) },
                    { "[3DSamples_v3]",      () => samples3D =         readArray(reader, () => new Sample3D()) },
                    { "[Effects]",           () => effects =           readArray(reader, () => new Effect()) },
                    { "[Effects_v2]",        () => effects2 =          readArray(reader, () => new EffectV2()) },
                    { "[VertexModifiers]",   () => vertexModifiers =   readArray(reader, () => new VertexModifier()) },
                    { "[TextureProperties]", () => textureProperties = readArray(reader, () => new TextureProperty()) },
                    { "[Behaviours]",        () => behaviors =         readArray(reader, () => new Behavior()) },
                    { "[Scene]",             () => sceneItems =        readArray(reader, () => new SceneItem()) },

                    { "[EOS]", () => shouldReadNext = false }
                };

                while (shouldReadNext)
                {
                    string sectionName = reader.ReadZString();
                    if (sectionHandlers.ContainsKey(sectionName))
                        sectionHandlers[sectionName]();
                    else
                        throw new InvalidDataException("Invalid scene section \"" + sectionName + "\"");
                }
            }

            private static void writeSingle<T>(BinaryWriter writer, T value, string sectionName) where T : ISceneSection
            {
                if (value == null)
                    return;
                writer.WriteZString(sectionName);
                value.Write(new GatekeeperStream(writer.BaseStream));
            }

            private static void writeArray<T>(BinaryWriter writer, T[] array, string sectionName) where T : ISceneSection 
            {
                if (array.Length == 0)
                    return;
                writer.WriteZString(sectionName);
                writer.Write((UInt32)array.Length);
                foreach (T section in array)
                    section.Write(new GatekeeperStream(writer.BaseStream, false));
            }

            public void Write(Stream stream)
            {
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.WriteZString("[Scenefile]");

                // write primitives
                writer.WriteZString("[SceneOrigin]");
                sceneOrigin.Write(writer);                
                writer.WriteZString("[AmbientSound]");
                writer.Write(ambientSound);
                writer.WriteZString("[Music]");
                writer.Write(music);
                if (backdropFile.Length > 0)
                {
                    writer.WriteZString("[Backdrop]");
                    writer.WriteZString(backdropFile);
                }

                // Write sections
                writeSingle(writer, version, "[Version]");
                writeSingle(writer, misc, "[Misc]");
                writeSingle(writer, waypointSystem, "[WaypointSystem]");
                writeSingle(writer, dataset, "[Dataset]");
                writeArray(writer, lights, "[Lights]");
                writeArray(writer, foModels, "[FOModels_v4]");
                writeArray(writer, models, "[Models_v3]");
                writeArray(writer, dynModels, "[DynamicModels]");
                writeArray(writer, triggers, "[Triggers]");
                writeArray(writer, samples2D, "[2DSamples_v2]");
                writeArray(writer, samples3D, "[3DSamples_v3]");
                writeArray(writer, effects, "[Effects]");
                writeArray(writer, effects2, "[Effects_v2]");
                writeArray(writer, vertexModifiers, "[VertexModifiers]");
                writeArray(writer, textureProperties, "[TextureProperties]");
                writeArray(writer, behaviors, "[Behaviours]");
                writeArray(writer, sceneItems, "[Scene]");

                writer.WriteZString("[EOS]");
            }
        }
    }
}