using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using zzio.primitives;

namespace zzio {
    namespace scn {
        [System.Serializable]
        public partial class Scene {
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
            public Behaviour[] behaviours;
            public SceneItem[] sceneItems;
            public UInt32 ambientSoundCount;
            public UInt32 musicCount;

            public Scene() {
                sceneOrigin = new Vector();
                backdropFile = null;
                ambientSoundCount = musicCount = 0;
            }

            public static Scene read(byte[] buffer) {
                Scene s = new Scene();
                BinaryReader reader = new BinaryReader(new MemoryStream(buffer, false));
                if (Utils.readZString(reader) != "[Scenefile]")
                    throw new InvalidDataException("Buffer is not a scene file");

                bool shouldReadNext = true;
                string sectionName;
                while (shouldReadNext) {
                    switch (sectionName = Utils.readZString(reader)) {
                        case ("[Version]"): { s.version = readVersion(reader); }break;
                        case ("[Misc]"): { s.misc = readMisc(reader); }break;
                        case ("[WaypointSystem]"): { s.waypointSystem = readWaypointSystem(reader); }break;
                        case ("[Dataset]"): { s.dataset = readDataset(reader);  }break;
                        case ("[SceneOrigin]"): { s.sceneOrigin = Vector.read(reader); }break;
                        case ("[Backdrop]"): { s.backdropFile = Utils.readZString(reader); }break;
                        case ("[Lights]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.lights = new Light[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.lights[i] = readLight(reader);
                            } break;
                        case ("[FOModels_v4]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.foModels = new FOModel[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.foModels[i] = readFOModel(reader);
                            } break;
                        case ("[Models_v3]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.models = new Model[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.models[i] = readModel(reader);
                            } break;
                        case ("[DynamicModels]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.dynModels = new DynModel[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.dynModels[i] = readDynModel(reader);
                            } break;
                        case ("[Triggers]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.triggers = new Trigger[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.triggers[i] = readTrigger(reader);
                            } break;
                        case ("[2DSamples_v2]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.samples2D = new Sample2D[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.samples2D[i] = readSample2D(reader);
                            } break;
                        case ("[3DSamples_v2]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.samples3D = new Sample3D[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.samples3D[i] = readSample3D(reader);
                            } break;
                        case ("[Effects]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.effects = new Effect[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.effects[i] = readEffect(reader);
                            } break;
                        case ("[Effects_v2]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.effects2 = new EffectV2[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.effects2[i] = readEffectV2(reader);
                            } break;
                        case ("[VertexModifiers]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.vertexModifiers = new VertexModifier[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.vertexModifiers[i] = readVertexModifier(reader);
                            } break;
                        case ("[TextureProperties]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.textureProperties = new TextureProperty[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.textureProperties[i] = readTexProperty(reader);
                            } break;
                        case ("[Behaviours]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.behaviours = new Behaviour[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.behaviours[i] = readBehaviour(reader);
                            } break;
                        case ("[Scene]"): {
                                UInt32 count = reader.ReadUInt32();
                                s.sceneItems = new SceneItem[count];
                                for (UInt32 i = 0; i < count; i++)
                                    s.sceneItems[i] = readSceneItem(reader);
                            } break;
                        case ("[AmbientSound]"): { s.ambientSoundCount = reader.ReadUInt32(); }break;
                        case ("[Music]"): { s.musicCount = reader.ReadUInt32(); }break;
                        case ("[EOS]"): { shouldReadNext = false; }break;
                        default: { throw new InvalidDataException("Invalid scene section \"" + sectionName + "\""); }
                    }
                }

                return s;
            }
        }
    }
}