using System;

namespace zzio.rwbs
{
    [Flags]
    public enum GeometryFormat
    {
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
}
