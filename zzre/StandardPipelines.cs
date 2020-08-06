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
    public struct ModelStandardTransformationUniforms
    {
        public Matrix4x4 projection;
        public Matrix4x4 view;
        public Matrix4x4 world;
        public static uint Stride = (3 * 4 * 4) * sizeof(float);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ModelStandardMaterialUniforms
    {
        public FColor tint;
        public float vertexColorFactor;
        public float tintFactor;
        public float alphaReference;
        public static uint Stride = (4 + 3) * sizeof(float);

        public static readonly ModelStandardMaterialUniforms Default = new ModelStandardMaterialUniforms
        {
            tint = FColor.White,
            vertexColorFactor = 1f,
            tintFactor = 1f,
            alphaReference = 0.03f
        };
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
                .With("TransformationBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)
                .With("MaterialBuffer", ResourceKind.UniformBuffer, ShaderStages.Fragment)
                .With(FrontFace.CounterClockwise)
                .Build());
        }
    }
}
