using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Veldrid;
using zzio.primitives;
using zzre.core;

namespace zzre
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ModelStandardVertex
    {
        public Vector3 pos;
        public Vector2 tex;
        public IColor color;
        public static uint Stride =
            (3 + 2) * sizeof(float) +
            4 * sizeof(byte);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ModelStandardUniforms
    {
        public Matrix4x4 projection;
        public Matrix4x4 view;
        public Matrix4x4 world;
        public Vector4 tint;
        public static uint Stride =
            (3 * 4 * 4) * sizeof(float) +
            4 * sizeof(float);
    }

    public class StandardPipelines
    {
        private readonly Lazy<IBuiltPipeline> modelStandard;
        public IBuiltPipeline ModelStandard => modelStandard.Value;

        public StandardPipelines(PipelineCollection collection)
        {
            modelStandard = new Lazy<IBuiltPipeline>(() => collection
                .GetPipeline()
                .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
                .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
                .WithShaderSet("ModelStandard")
                .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
                .With("TexCoords", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
                .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
                .With("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
                .With("Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
                .With("UniformBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment)
                .With(FrontFace.CounterClockwise)
                .Build());
        }
    }
}
