using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio;
using zzre.rendering;

namespace zzre.materials
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DebugWireframeVertex
    {
        public Vector3 pos;
        public Vector3 tex;

        public static uint Stride = (3 + 3) * sizeof(float);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DebugWireframeUniforms
    {
        public FColor color;
        public float width;
        public static uint Stride = (4 + 1) * sizeof(float);

        public static readonly DebugWireframeUniforms Default = new()
        {
            color = FColor.White,
            width = 4f
        };
    }

    public class DebugWireframeMaterial : BaseMaterial, IStandardTransformMaterial
    {
        public UniformBinding<Matrix4x4> Projection { get; }
        public UniformBinding<Matrix4x4> View { get; }
        public UniformBinding<Matrix4x4> World { get; }
        public UniformBinding<DebugWireframeUniforms> Uniforms { get; }

        public DebugWireframeMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer))
        {
            Configure()
                .Add(Projection = new UniformBinding<Matrix4x4>(this))
                .Add(View = new UniformBinding<Matrix4x4>(this))
                .Add(World = new UniformBinding<Matrix4x4>(this))
                .Add(Uniforms = new UniformBinding<DebugWireframeUniforms>(this))
                .NextBindingSet();
        }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer) => PipelineFor<DebugMaterial>.Get(diContainer, builder => builder
            .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
            .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
            .WithShaderSet("Wireframe")
            .With("Position", VertexElementFormat.Float3, VertexElementSemantic.Position)
            .With("UV", VertexElementFormat.Float4, VertexElementSemantic.Color)
            .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("World", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With("Params", ResourceKind.UniformBuffer, ShaderStages.Fragment)
            .With(BlendStateDescription.SingleOverrideBlend)
            .WithDepthWrite(true)
            .WithDepthTest(true)
            .With(FaceCullMode.None)
            .Build());
    }
}
