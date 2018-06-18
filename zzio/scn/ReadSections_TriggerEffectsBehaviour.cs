using System;
using System.IO;
using Newtonsoft.Json;
using zzio.primitives;

namespace zzio {
    namespace scn {
        [System.Serializable]
        public enum TriggerType
        {
            Doorway = 0,
            SingleplayerStartpoint,
            MultiplayerStartpoint,
            NpcStartpoint,
            CameraPosition,
            Waypoint,
            StartDuel, //unused
            LeaveDuel, //unused
            NpcAttackPosition,
            FlyArea, //unused
            KillPlayer,
            SetCameraView, //unused
            SavePoint, //unused
            SwampMarker,
            RiverMarker,
            PlayVideo, //unused
            Elevator, //in the executable it is actually called teleporter
            GettingACard, //unused
            Sign,
            GettingPixie, //unused
            UsingPipe, //unused
            DancePlatform, //unused
            LeaveDancePlatform, //unused
            RemoveStoneBlocker, //unused
            RemovePlantBlocker, //unused
            EventCamera,
            Platform,
            CreatePlatforms,
            ShadowLight,
            CreateItems,
            Item,
            Shrink, //unused
            WizformMarker, //unused
            //RemoveLock, //this name is in the executable, but it messes up with the rest of the types
            IndoorCamera, //unused
            LensFlare, //unused
            FogModifier,
            OpenMagicWaypoints, //unused
            RuneTarget, //no name present in the executable
            Unused38, //no name present in the executable
            Animal,
            AnimalWaypoint,
            SceneOpening, //unused
            CollectionWizform,
            ElementalLock,
            ItemGenerator,
            Escape,
            Jumper,
            RefreshMana, //unused
            StartSubgame, //unused
            TemporaryNpc,
            EffectBeam,
            MultiplayerObserverPosition,
            MultiplayerHealingPool,
            MultiplayerManaPool,
            Ceiling,
            HealAllWizforms,

            Unknown = -1
        }

        [System.Serializable]
        public enum TriggerColliderType {
            Box = 0,
            Sphere = 1,
            Point = 2,

            Unknown = -1
        }

        [System.Serializable]
        public enum EffectType { //I am truly sorry
            Unknown1 = 1,
            Unknown4 = 4,
            Unknown5 = 5,
            Unknown6 = 6,
            Unknown7 = 7,
            Unknown10 = 10,
            Unknown13 = 13,

            Unknown = -1
        }

        [System.Serializable]
        public enum EffectV2Type {
            Unknown1 = 1,
            Unknown6 = 6,
            Unknown10 = 10,
            Snowflakes = 11,
            Unknown13 = 13,

            Unknown = -1
        }

        [System.Serializable]
        public enum BehaviourType {
            Cloud = 800,
            SmpleDoorLeft = 900,
            SimpleDoorRight = 901,
            MetalDoorLeft = 902,
            CityDoorDown = 903,
            CityDoorUp = 904,
            DoorGold = 910,
            DoorSilver = 911,
            DoorCopper = 912,
            DoorBronze = 913,
            DoorIron = 914,
            DoorPlating = 915,
            DoorGlass = 916,
            DoorWithLock = 917,
            CityDoorLock = 918,
            LockedMetalDoor = 919,
            LockedWoodenDoor = 920,
            DoorRed = 921,
            DoorYellow = 922,
            DoorBlue = 923,
            Collectable = 1000,
            Collectable_EFF0 = 1001,
            Collectable_EFF1 = 1002,
            Lock = 1100,
            BlockerStone = 1500,
            BlockerPlant = 1501,
            MagicBridgeStatic = 1502,
            IronGate = 1503,
            MagicBridge0 = 1504,
            MagicBridge1 = 1505,
            MagicBridge2 = 1506,
            LookAtPlayer = 2000,
            Swing = 2001,
            FlameAnimation = 2002,
            SkyMovement = 2003,
            River2 = 2004,
            River3 = 2005,
            River4 = 2006,
            WaterAnimation = 2007,
            YRotate1 = 2008,
            YRotate2 = 2009,
            Bird = 2010,
            TextureWobble = 2011,
            SimpleCorona = 2012,
            BirdCircle = 2013,
            ZRotate1 = 2014,
            ZRotate2 = 2015,
            XRotate1 = 2016,
            XRotate2 = 2017,
            River5 = 2018,
            River6 = 2019,
            River7 = 2020,
            River8 = 2021,
            FlyAwayVisible = 2100,
            FlyAwayInvisible = 2101,
            FlyAwayStraight = 2102,

            Unknown = -1
        }

        [System.Serializable]
        public struct Trigger {
            public UInt32 idx, normalizeDir;
            public TriggerColliderType colliderType;
            public Vector dir;
            public TriggerType type;
            public UInt32 ii1, ii2, ii3, ii4;
            public string s;
            public Vector pos,
                size; //only if type == Box
            public float radius; //only if type == Sphere
        }

        [System.Serializable]
        public struct Effect {
            public UInt32 idx;
            public EffectType type;
            public Vector v1, v2, v3;
            public UInt32 param;
            public string effectFile;
        }

        [System.Serializable]
        public struct EffectV2 {
            public UInt32 idx;
            public EffectV2Type type;
            public UInt32 i1, i2, i3, i4, i5;
            public Vector v1, v2, v3;
            public UInt32 param;
            public string s;
        }

        [System.Serializable]
        public struct Behaviour {
            public BehaviourType type;
            public UInt32 modelId;
        }

        public partial class Scene {
            private static Trigger readTrigger(BinaryReader reader) {
                Trigger t;
                t.idx = reader.ReadUInt32();
                t.colliderType = Utils.intToEnum<TriggerColliderType>(reader.ReadInt32());
                t.normalizeDir = reader.ReadUInt32();
                t.dir = Vector.read(reader);
                t.type = Utils.intToEnum<TriggerType>(reader.ReadInt32());
                t.ii1 = reader.ReadUInt32();
                t.ii2 = reader.ReadUInt32();
                t.ii3 = reader.ReadUInt32();
                t.ii4 = reader.ReadUInt32();
                t.s = Utils.readZString(reader);
                t.pos = Vector.read(reader);
                t.size = new Vector();
                t.radius = 0.0f;
                switch(t.colliderType) {
                    case (TriggerColliderType.Box): {
                            t.size = Vector.read(reader);
                        }break;
                    case (TriggerColliderType.Sphere): {
                            t.radius = reader.ReadSingle();
                        }break;
                    case (TriggerColliderType.Point): {}break;
                    default: { throw new InvalidDataException("Invalid trigger type"); }
                }
                return t;
            }

            private static Effect readEffect(BinaryReader reader) {
                Effect e;
                e.idx = reader.ReadUInt32();
                e.type = Utils.intToEnum<EffectType>(reader.ReadInt32());
                e.v1 = e.v2 = e.v3 = new Vector();
                e.param = 0;
                e.effectFile = null;
                switch(e.type) {
                    case (EffectType.Unknown1):
                    case (EffectType.Unknown5):
                    case (EffectType.Unknown6):
                    case (EffectType.Unknown10): {
                            e.param = reader.ReadUInt32();
                            e.v1 = Vector.read(reader);
                            e.v2 = Vector.read(reader);
                        }break;
                    case (EffectType.Unknown4): {
                            e.param = reader.ReadUInt32();
                            e.v1 = Vector.read(reader);
                        }break;
                    case (EffectType.Unknown7): {
                            e.effectFile = Utils.readZString(reader);
                            e.v1 = Vector.read(reader);
                        }break;
                    case (EffectType.Unknown13): {
                            e.effectFile = Utils.readZString(reader);
                            e.v1 = Vector.read(reader);
                            e.v2 = Vector.read(reader);
                            e.v3 = Vector.read(reader);
                            e.param = reader.ReadUInt32();
                        }break;
                    default: { throw new InvalidDataException("Invalid effect type"); }
                }
                return e;
            }

            private static EffectV2 readEffectV2(BinaryReader reader) {
                EffectV2 e;
                e.idx = reader.ReadUInt32();
                e.type = Utils.intToEnum<EffectV2Type>(reader.ReadInt32());
                e.i1 = reader.ReadUInt32();
                e.i2 = reader.ReadUInt32();
                e.i3 = reader.ReadUInt32();
                e.i4 = reader.ReadUInt32();
                e.i5 = reader.ReadUInt32();
                e.v1 = e.v2 = e.v3 = new Vector();
                e.s = null;
                switch(e.type) {
                    case (EffectV2Type.Unknown1):
                    case (EffectV2Type.Unknown6):
                    case (EffectV2Type.Unknown10): {
                            e.param = reader.ReadUInt32();
                            e.v1 = Vector.read(reader);
                            e.v2 = Vector.read(reader);
                        }break;
                    case (EffectV2Type.Snowflakes): {
                            e.param = reader.ReadUInt32();
                        }break;
                    case (EffectV2Type.Unknown13): {
                            e.s = Utils.readZString(reader);
                            e.v1 = Vector.read(reader);
                            e.v2 = Vector.read(reader);
                            e.v3 = Vector.read(reader);
                            e.param = reader.ReadUInt32();
                        }break;
                    default: { throw new InvalidDataException("Invalid effect v2 type"); }
                }
                return e;
            }

            private static Behaviour readBehaviour(BinaryReader reader) {
                Behaviour b;
                b.type = Utils.intToEnum<BehaviourType>(reader.ReadInt32());
                b.modelId = reader.ReadUInt32();
                return b;
            }
        }
    }
}
