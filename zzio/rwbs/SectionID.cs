﻿namespace zzio {
    namespace rwbs {
        //Source: http://www.gtamodding.com/wiki/List_of_RW_section_IDs
        public enum SectionId {
            Struct = 0x0001,
            String = 0x0002,
            Extension = 0x0003,
            Camera = 0x0005,
            Texture = 0x0006,
            Material = 0x0007,
            MaterialList = 0x0008,
            AtomicSection = 0x0009,
            PlaneSection = 0x000A,
            World = 0x000B,
            Spline = 0x000C,
            Matrix = 0x000D,
            FrameList = 0x000E,
            Geometry = 0x000F,
            Clump = 0x0010,
            Light = 0x0012,
            UnicodeString = 0x0013,
            Atomic = 0x0014,
            TextureNative = 0x0015,
            TextureDictionary = 0x0016,
            AnimationDatabase = 0x0017,
            Image = 0x0018,
            SkinAnimation = 0x0019,
            GeometryList = 0x001A,
            AnimAnimation = 0x001B,
            Team = 0x001C,
            Crowd = 0x001D,
            DeltaMorphAnimation = 0x001E,
            RightToRender = 0x001F,
            MultiTexEffectNative = 0x0020,
            MultiTexEffectDictionary = 0x0021,
            TeamDictionary = 0x0022,
            PITextureDictionary = 0x0023, //PI = Platform Independent
            TableOfContents = 0x0024,
            ParticleStdGlobalData = 0x0025,
            AltPipe = 0x0026,
            PIPeds = 0x0027,
            PatchMesh = 0x0028,
            ChunkGroupStart = 0x0029,
            ChunkGroupEnd = 0x002A,
            UVAnimationDictionary = 0x002B,
            CollTree = 0x002C,

            //Plugin
            MetricsPLG = 0x0101,
            SplinePLG = 0x0102,
            StereoPLG = 0x0103,
            VRMLPLG = 0x0104,
            MorphPLG = 0x0105,
            PVSPLG = 0x0106,
            MemoryLeakPLG = 0x0107,
            AnimationPLG = 0x0108,
            GlossPLG = 0x0109,
            LogoPLG = 0x010A,
            MemoryInfoPLG = 0x010B,
            RandomPLG = 0x010C,
            PNGImagePLG = 0x010D,
            BonePLG = 0x010E,
            VRMLAnimPLG = 0x010F,
            SkyMipmapVal = 0x0110,
            MRMPLG = 0x0111,
            LODAtomicPLG = 0x0112,
            MEPLG = 0x0113,
            LightmapPLG = 0x0114,
            RefinePLG = 0x0115,
            SkinPLG = 0x0116,
            LabelPLG = 0x0117,
            ParticlesPLG = 0x0118,
            GeomTXPLG = 0x0119,
            SynthCorePLG = 0x011A,
            STQPPPLG = 0x011B,
            PartPPPLG = 0x011C,
            CollisionPLG = 0x011D,
            HAnimPLG = 0x011E,
            UserDaaPLG = 0x011F,
            MaterialEffectsPLG = 0x0120,
            ParticleSystemPLG = 0x0121,
            DeltaMorphPLG = 0x0122,
            PatchPLG = 0x0123,
            TeamPLG = 0x0124,
            CrowdPPPLG = 0x0125,
            MipSplitPLG = 0x0126,
            AnisotropyPLG = 0x0127,
            GCNMaterialPLG = 0x0129,
            GeometricPVSPLG = 0x012A,
            XBOXMaterialPLG = 0x012B,
            MultiTexturePLG = 0x0124C,
            ChainPLG = 0x012D,
            ToonPLG = 0x012E,
            PTankPLG = 0x012F,
            ParticleStdPLG = 0x0130,
            PDSPLG = 0x0131,
            PrtAdvPLG = 0x0132,
            NormalMapPLG = 0x0133,
            ADCPLG = 0x0134,
            UVAnimationPLG = 0x0135,
            CharacterSetPLG = 0x0180,
            NOHSWorldPLG = 0x0171,
            ImportUtilPLG = 0x0182,
            SlerpPLG = 0x0183,
            OptimPLG = 0x0184,
            TLWorldPLG = 0x0185,
            DatabasePLG = 0x0186,
            RaytracePLG = 0x0187,
            RayPLG = 0x0188,
            LibraryPLG = 0x0189,
            TWODPLG = 0x0190,
            TileRenderPLG = 0x0191,
            JPEGImagePLG = 0x0192,
            TGAImagePLG = 0x0193,
            GIFImagePLG = 0x0194,
            QuatPLG = 0x0195,
            SplinePVSPLG = 0x0196,
            MipmapPLG = 0x0197,
            MipmapKPLG = 0x0198,
            TWODFont = 0x0199,
            IntersectionPLG = 0x019A,
            TIFFImagePLG = 0x019B,
            PickPLG = 0x019C,
            BMPImagePLG = 0x019D,
            RASImagePLG = 0x019E,
            SkinFXPLG = 0x019F,
            VCATPLG = 0x01A0,
            TWODPath = 0x01A1,
            TWODBrush = 0x01A2,
            TWODObject = 0x01A3,
            TWODShape = 0x01A4,
            TWODScene = 0x01A5,
            TWODPickRegion = 0x01A6,
            TWODObjectString = 0x01A7,
            TWODAnimationPLG = 0x01A8,
            TWODAnimation = 0x01A9,
            TWODKeyframe = 0x01B0,
            TWODMaestro = 0x01B1,
            Barycentric = 0x01B2,
            PITextureDictionaryTK = 0x01B3,
            TOCTK = 0x01B4,
            TPLTK = 0x01B5,
            AltPipeTK = 0x01B6,
            AnimationTK = 0x01B7,
            SkinSplitToolkit = 0x01B8,
            CompressedKeyTK = 0x01B9,
            GeometryConditioningPLG = 0x01BA,
            WingPLG = 0x01BB,
            GenericPipelineTK = 0x01BC,
            LightmapConversionTK = 0x01BD,
            FilesystemPLG = 0x01BE,
            DictionaryTK = 0x01BF,
            UVAnimationLinear = 0x01C0,
            UVAnimationParameter = 0x01C1,

            BinMeshPLG = 0x050E,
            NativeDataPLG = 0x0510,
            ZModelerLock = 0xF21E,

            //yes I skipped the Rockstar Custom Sections at now

            Unknown = -1
        }
    }
}