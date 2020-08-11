using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using zzio.primitives;

namespace zzre.materials
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ColoredVertex
    {
        public Vector3 pos;
        public IColor color;
        public static uint Stride = 3 * sizeof(float) + 4 * sizeof(byte);

        public ColoredVertex(Vector3 pos, IColor color)
        {
            this.pos = pos;
            this.color = color;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SkinVertex
    {
        public Vector4 weights;
        public byte bone0, bone1, bone2, bone3;
        public static uint Stride = 4 * sizeof(float) + 4 * sizeof(byte);
    }
}
