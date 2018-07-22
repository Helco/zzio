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
            public Version version;
            public Misc misc;
            public WaypointSystem waypointSystem;
            public Dataset dataset;
            public Vector sceneOrigin;
            public string backdropFile;
            public Light[] lights;
            public Model[] models;
            public FOModel[] foModels;
            public DynModel[] dynModels;
            public Trigger[] triggers;
            public Sample2D[] samples2D;
            public Sample3D[] samples3D;
            public Effect[] effects;
            public EffectV2[] effects2;
            public VertexModifier[] vertexModifiers;
            public TextureProperty[] textureProperties;
            public Behavior[] behaviors;
            public SceneItem[] sceneItems;
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

            public void Read(Stream stream, bool shouldClose = true)
            {
                bool shouldReadNext = true;
                BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, !shouldClose);
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
        }
    }
}