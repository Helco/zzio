using System;

namespace zzio {
    namespace rwbs {
        public enum TextureFilterMode {
            NAFilterMode = 0,
            Nearest = 1,
            Linear = 2,
            MipNearest = 3,
            MipLinear = 4,
            LinearMipNearest = 5,
            LinearMipLinear = 6,

            Unknown = -1
        }

        public enum TextureAddressingMode {
            NATextureAddress = 0,
            Wrap = 1,
            Mirror = 2,
            Clamp = 3,
            Border = 4,

            Unknown = -1
        }

        public enum BinMeshFlags {
            TriList = 0,
            TriStrip = 1,

            Unknown = -1
        }

        public enum AnimSubDataType {
            Type1 = 1,
            Type2 = 2,

            Unknown = -1
        }

        [Flags]
        public enum GeometryFormat {
            TriStrip = 0x00000001,
            Positions = 0x00000002,
            Textured = 0x00000004,
            Prelit = 0x00000008,
            Normals = 0x00000010,
            Light = 0x00000020,
            ModMaterialColor = 0x00000040,
            Textured2 = 0x00000080,
            Native = 0x01000000,
            NativeInstance = 0x02000000,
            SectorsOverlap = 0x40000000
        }

        [Flags]
        public enum AtomicFlags {
            CollisionTest = 0x01,
            Render = 0x02
        }

        public enum BoneFlags {
            ParentNoSibling = 8,
            NoParentNoSibling = 9,
            ParentAndSibling = 10,

            Unknown = -1
        }
    }
}