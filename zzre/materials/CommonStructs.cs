using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace zzre.materials
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TransformUniforms
    {
        public Matrix4x4 projection;
        public Matrix4x4 view;
        public Matrix4x4 world;
        public static uint Stride = (3 * 4 * 4) * sizeof(float);
    }
}
