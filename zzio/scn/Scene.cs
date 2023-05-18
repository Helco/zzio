using System;
using System.IO;
using System.Collections.Generic;
using zzio;
using System.Numerics;
using System.Linq;

namespace zzio
{
    namespace scn
    {
        [Serializable]
        public partial class Scene
        {
            public Version version = new();
            public Misc misc = new();
            public WaypointSystem waypointSystem = new();
            public Dataset dataset = new();
            public Vector3 sceneOrigin;
            public string backdropFile = "";
            public Light[] lights = [];
            public Model[] models = [];
            public FOModel[] foModels = [];
            public DynModel[] dynModels = [];
            public Trigger[] triggers = [];
            public Sample2D[] samples2D = [];
            public Sample3D[] samples3D = [];
            public SceneEffect[] effects = [];
            public VertexModifier[] vertexModifiers = [];
            public TextureProperty[] textureProperties = [];
            public Behavior[] behaviors = [];
            public SceneItem[] sceneItems = [];
            public uint ambientSound;
            public uint music;

            private static T[] readArray<T>(BinaryReader reader, Func<T> ctor) where T : ISceneSection
            {
                uint count = reader.ReadUInt32();
                T[] result = new T[count];
                for (uint i = 0; i < count; i++)
                    (result[i] = ctor()).Read(new GatekeeperStream(reader.BaseStream));
                return result;
            }

            public void Read(Stream stream)
            {
                bool shouldReadNext = true;
                using BinaryReader reader = new(stream);
                if (reader.ReadZString() != "[Scenefile]")
                    throw new InvalidDataException("Magic section name missing in scene file");

                var effectsV1 = Array.Empty<SceneEffect>();
                var effectsV2 = Array.Empty<SceneEffect>();
                Dictionary<string, Action> sectionHandlers = new()
                {
                    { "[Version]",           () => (version =          new Version()).Read(new GatekeeperStream(stream)) },
                    { "[Misc]",              () => (misc =             new Misc()).Read(new GatekeeperStream(stream)) },
                    { "[WaypointSystem]",    () => (waypointSystem =   new WaypointSystem()).Read(new GatekeeperStream(stream)) },
                    { "[Dataset]",           () => (dataset =          new Dataset()).Read(new GatekeeperStream(stream)) },
                    { "[SceneOrigin]",       () => sceneOrigin =       reader.ReadVector3() },
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
                    { "[Effects]",           () => effectsV1 =         readArray(reader, () => new SceneEffect(SceneEffectReadVersion.V1)) },
                    { "[Effects_v2]",        () => effectsV2 =         readArray(reader, () => new SceneEffect(SceneEffectReadVersion.V2)) },
                    { "[VertexModifiers]",   () => vertexModifiers =   readArray(reader, () => new VertexModifier()) },
                    { "[TextureProperties]", () => textureProperties = readArray(reader, () => new TextureProperty()) },
                    { "[Behaviours]",        () => behaviors =         readArray(reader, () => new Behavior()) },
                    { "[Scene]",             () => sceneItems =        readArray(reader, () => new SceneItem()) },

                    { "[EOS]", () => shouldReadNext = false }
                };

                while (shouldReadNext)
                {
                    string sectionName = reader.ReadZString();
                    if (sectionHandlers.TryGetValue(sectionName, out var readSection))
                        readSection();
                    else
                        throw new InvalidDataException("Invalid scene section \"" + sectionName + "\"");
                }
                effects = [.. effectsV1, .. effectsV2];
            }

            private static void writeSingle<T>(BinaryWriter writer, T value, string sectionName) where T : ISceneSection
            {
                if (value == null)
                    return;
                writer.WriteZString(sectionName);
                value.Write(new GatekeeperStream(writer.BaseStream));
            }

            private static void writeArray<T>(BinaryWriter writer, IEnumerable<T> array, string sectionName) where T : ISceneSection
            {
                if (!array.Any())
                    return;
                writer.WriteZString(sectionName);
                writer.Write((uint)array.Count());
                foreach (T section in array)
                    section.Write(new GatekeeperStream(writer.BaseStream, false));
            }

            public void Write(Stream stream)
            {
                using BinaryWriter writer = new(stream);
                writer.WriteZString("[Scenefile]");
                writeSingle(writer, version, "[Version]");
                writeSingle(writer, misc, "[Misc]");
                writeArray(writer, lights, "[Lights]");
                writeArray(writer, foModels, "[FOModels_v4]");
                writeArray(writer, models, "[Models_v3]");
                writeArray(writer, dynModels, "[DynamicModels]");
                writeArray(writer, triggers, "[Triggers]");
                writeArray(writer, samples3D, "[3DSamples_v2]");
                writeArray(writer, samples2D, "[2DSamples_v2]");
                writer.WriteZString("[AmbientSound]");
                writer.Write(ambientSound);
                writer.WriteZString("[Music]");
                writer.Write(music);
                writeArray(writer, effects.Where(e => e.ReadVersion is SceneEffectReadVersion.V1), "[Effects]");
                writeArray(writer, effects.Where(e => e.ReadVersion is SceneEffectReadVersion.V2), "[Effects_v2]");
                writeArray(writer, sceneItems, "[Scene]");
                writeArray(writer, vertexModifiers, "[VertexModifiers]");
                writeArray(writer, behaviors, "[Behaviours]");
                writeSingle(writer, dataset, "[Dataset]");
                writer.WriteZString("[SceneOrigin]");
                writer.Write(sceneOrigin);
                writeArray(writer, textureProperties, "[TextureProperties]");
                writeSingle(writer, waypointSystem, "[WaypointSystem]");

                if (backdropFile.Length > 0)
                {
                    writer.WriteZString("[Backdrop]");
                    writer.WriteZString(backdropFile);
                }
                writeArray(writer, effects, "[Effects]");
                writer.WriteZString("[EOS]");
            }
        }
    }
}
