using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using zzio;
using zzre.rendering;

namespace zzre.materials
{
    [StructLayout(LayoutKind.Sequential)]
    public struct UIInstance
    {
        public Vector2 pos, size;
        public Vector2 uvPos, uvSize;
        public float textureWeight;
        public IColor color;

        public const uint Stride = sizeof(float) * (4 * 2 + 1) + sizeof(int) + sizeof(byte) * 4;
    }

    public class UIMaterial : BaseMaterial
    {
        public TextureBinding Texture { get; }
        public SamplerBinding Sampler { get; }
        public UniformBinding<Vector2> ScreenSize { get; }

        public UIMaterial(ITagContainer diContainer, bool isFont) : base(diContainer.GetTag<GraphicsDevice>(), GetPipeline(diContainer, isFont))
        {
            Configure()
                .Add(Texture = new TextureBinding(this))
                .Add(Sampler = new SamplerBinding(this))
                .Add(ScreenSize = new UniformBinding<Vector2>(this))
                .NextBindingSet();
        }

        private static IBuiltPipeline GetPipeline(ITagContainer diContainer, bool isFont)
        {
            System.Func<IPipelineBuilder, IBuiltPipeline> bla = builder => builder
            .WithDepthTarget(PixelFormat.D24_UNorm_S8_UInt)
            .WithColorTarget(PixelFormat.R8_G8_B8_A8_UNorm)
            .WithShaderSet(isFont ? "UIFont" : "UI")
            .WithInstanceStepRate(1)
            .With("Pos", VertexElementFormat.Float2, VertexElementSemantic.Position)
            .With("Size", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
            .With("UVPos", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
            .With("UVSize", VertexElementFormat.Float2, VertexElementSemantic.TextureCoordinate)
            .With("TextureWeight", VertexElementFormat.Float1, VertexElementSemantic.TextureCoordinate)
            .With("Color", VertexElementFormat.Byte4_Norm, VertexElementSemantic.Color)
            .With("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
            .With("Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
            .With("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            .With(BlendStateDescription.SingleAlphaBlend)
            .With(PrimitiveTopology.TriangleStrip)
            .WithDepthWrite(false)
            .WithDepthTest(false)
            .With(FaceCullMode.None)
            .Build();

            return isFont
                ? PipelineFor<int>.Get(diContainer, bla)
                : PipelineFor<UIMaterial>.Get(diContainer, bla);
        }
    }
}
