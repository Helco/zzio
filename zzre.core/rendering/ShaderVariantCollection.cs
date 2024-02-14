using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mlang;
using Mlang.Model;
using Veldrid;

using VeldridBlendFactor = Veldrid.BlendFactor;
using MlangBlendFactor = Mlang.Model.BlendFactor;
using VeldridBlendFunction = Veldrid.BlendFunction;
using MlangBlendFunction = Mlang.Model.BlendFunction;
using VeldridComparisonKind = Veldrid.ComparisonKind;
using MlangComparisonKind = Mlang.Model.ComparisonKind;
using VeldridStencilOperation = Veldrid.StencilOperation;
using MlangStencilOperation = Mlang.Model.StencilOperation;
using VeldridPixelFormat = Veldrid.PixelFormat;
using MlangPixelFormat = Mlang.Model.PixelFormat;
using VeldridPrimitiveTopology = Veldrid.PrimitiveTopology;
using MlangPrimitiveTopology = Mlang.Model.PrimitiveTopology;
using VeldridFrontFace = Veldrid.FrontFace;
using MlangFrontFace = Mlang.Model.FrontFace;
using VeldridCullMode = Veldrid.FaceCullMode;
using MlangCullMode = Mlang.Model.FaceCullMode;
using VeldridFillMode = Veldrid.PolygonFillMode;
using MlangFillMode = Mlang.Model.FaceFillMode;

namespace zzre.rendering;

public interface IBuiltPipeline
{
    Pipeline Pipeline { get; }
    ShaderVariant ShaderVariant { get; }
    IReadOnlyList<ResourceLayout> ResourceLayouts { get; }
}

public class ShaderVariantCollection : zzio.BaseDisposable
{
    private sealed class BuiltPrograms
    {
        public required Shader Vertex { get; init; }
        public required Shader Fragment { get; init; }
        // we might have to add a reference count to dispose shaders more quickly 
        // but for now we only unload everything at once upon disposal of the collection
    }

    private sealed class BuiltPipeline : IBuiltPipeline
    {
        public required ShaderVariant ShaderVariant { get; init; }
        public required Pipeline Pipeline { get; init; }
        public required IReadOnlyList<ResourceLayout> ResourceLayouts { get; init; }
    }

    private readonly IShaderSet shaderSet;
    private readonly Dictionary<ShaderVariantKey, BuiltPrograms> builtPrograms = [];
    private readonly Dictionary<ShaderVariantKey, BuiltPipeline> builtPipelines = [];

    public GraphicsDevice Device { get; }
    public ResourceFactory Factory => Device.ResourceFactory;

    public ShaderVariantCollection(GraphicsDevice device, Stream shaderSetStream)
    {
        shaderSet = new FileShaderSet(shaderSetStream);
        Device = device;
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        foreach (var s in builtPipelines.Values)
        {
            s.Pipeline.Dispose();
            foreach (var resLayout in s.ResourceLayouts)
                resLayout.Dispose();
        }
        foreach (var p in builtPrograms.Values)
        {
            p.Vertex.Dispose();
            p.Fragment.Dispose();
        }
        builtPipelines.Clear();
        builtPrograms.Clear();
        shaderSet.Dispose();
    }

    public ShaderInfo GetShaderInfo(string name) => shaderSet.GetShaderInfo(name);

    public IBuiltPipeline GetBuiltPipeline(ShaderVariantKey variantKey)
    {
        if (builtPipelines.TryGetValue(variantKey, out var builtShader))
            return builtShader;

        var variant = shaderSet.GetVariant(variantKey);
        var programs = GetBuiltPrograms(variant);
        var resourceLayouts = CreateResourceLayouts(variant);
        var pipeline = Factory.CreateGraphicsPipeline(new GraphicsPipelineDescription
        {
            BlendState = CreateBlendState(variant.PipelineState),
            DepthStencilState = CreateDepthStencilState(variant.PipelineState),
            Outputs = CreateOutputDescription(variant.PipelineState),
            PrimitiveTopology = CreatePrimitiveTopology(variant.PipelineState.PrimitiveTopology),
            RasterizerState = CreateRasterizerState(variant.PipelineState),
            ResourceLayouts = resourceLayouts,
            ShaderSet = new()
            {
                Shaders = [programs.Vertex, programs.Fragment],
                Specializations = [],
                VertexLayouts = CreateVertexLayouts(variant)
            }
        });
        pipeline.Name = $"Pipeline {variantKey}";
        var builtPipeline = new BuiltPipeline()
        {
            ShaderVariant = variant,
            ResourceLayouts = resourceLayouts,
            Pipeline = pipeline
        };
        builtPipelines.Add(variantKey, builtPipeline);
        return builtPipeline;
    }

    private BuiltPrograms GetBuiltPrograms(ShaderVariant variant)
    {
        var variantKey = variant.VariantKey;
        var shaderInfo = shaderSet.GetShaderInfo(variantKey.ShaderHash);
        var invariantKey = shaderInfo.GetProgramInvariantKey(variantKey);
        if (builtPrograms.TryGetValue(invariantKey, out var prevPrograms))
            return prevPrograms;

        var newPrograms = new BuiltPrograms()
        {
            Vertex = Factory.CreateShader(new(ShaderStages.Vertex, variant.VertexShader.ToArray(), "main")),
            Fragment = Factory.CreateShader(new(ShaderStages.Fragment, variant.FragmentShader.ToArray(), "main"))
        };
        newPrograms.Vertex.Name = $"Vertex {invariantKey}";
        newPrograms.Fragment.Name = $"Fragment {invariantKey}";
        builtPrograms.Add(invariantKey, newPrograms);
        return newPrograms;
    }

    private ResourceLayout[] CreateResourceLayouts(ShaderVariant variant)
    {
        var layouts = new ResourceLayoutDescription[variant.BindingSetSizes.Count];
        for (int i = 0; i < layouts.Length; i++)
        {
            var bindingCount = variant.BindingSetSizes[i];
            layouts[i] = new(new ResourceLayoutElementDescription[bindingCount]);
        }
        foreach (var binding in variant.Bindings)
        {
            var kind = binding.Type switch
            {
                StructureType => ResourceKind.UniformBuffer,
                BufferType => ResourceKind.StructuredBufferReadOnly,
                ImageType => ResourceKind.TextureReadOnly,
                SamplerType => ResourceKind.Sampler,
                _ => throw new NotImplementedException($"Unimplemented binding type: {binding.Type?.GetType()}")
            };
            layouts[binding.SetIndex].Elements[binding.BindingIndex] = new(
                binding.Name, kind, ShaderStages.Fragment | ShaderStages.Vertex);
        }
        return layouts.Select(Factory.CreateResourceLayout).ToArray();
    }

    private BlendStateDescription CreateBlendState(PipelineState state) => new()
    {
        AlphaToCoverageEnabled = state.AlphaToCoverage,
        BlendFactor = state.BlendFactor.ToRgbaFloat(),
        AttachmentStates = state.BlendAttachments.Select(CreateBlendAttachment).ToArray()
    };

    private BlendAttachmentDescription CreateBlendAttachment(BlendAttachment attachment)
    {
        if (attachment.Color == null)
            return BlendAttachmentDescription.Disabled;
        var color = attachment.Color.Value;
        var alpha = (attachment.Alpha ?? attachment.Color).Value;
        return new()
        {
            BlendEnabled = true,
            SourceColorFactor = CreateBlendFactor(color.Source),
            DestinationColorFactor = CreateBlendFactor(color.Destination),
            ColorFunction = CreateBlendFunction(color.Function),
            SourceAlphaFactor = CreateBlendFactor(alpha.Source),
            DestinationAlphaFactor = CreateBlendFactor(alpha.Destination),
            AlphaFunction = CreateBlendFunction(alpha.Function)
        };
    }

    private static VeldridBlendFactor CreateBlendFactor(MlangBlendFactor f) => f switch
    {
        MlangBlendFactor.BlendFactor => VeldridBlendFactor.BlendFactor,
        MlangBlendFactor.SrcColor => VeldridBlendFactor.SourceColor,
        MlangBlendFactor.SrcAlpha => VeldridBlendFactor.SourceAlpha,
        MlangBlendFactor.DstColor => VeldridBlendFactor.DestinationColor,
        MlangBlendFactor.DstAlpha => VeldridBlendFactor.DestinationAlpha,
        MlangBlendFactor.InvBlendFactor => VeldridBlendFactor.InverseBlendFactor,
        MlangBlendFactor.InvDstAlpha => VeldridBlendFactor.InverseDestinationAlpha,
        MlangBlendFactor.InvDstColor => VeldridBlendFactor.InverseDestinationColor,
        MlangBlendFactor.InvSrcAlpha => VeldridBlendFactor.InverseSourceAlpha,
        MlangBlendFactor.InvSrcColor => VeldridBlendFactor.InverseSourceColor,
        MlangBlendFactor.One => VeldridBlendFactor.One,
        MlangBlendFactor.Zero => VeldridBlendFactor.Zero,
        _ => throw new NotImplementedException($"Unimplemented Mlang blend factor: {f}")
    };

    private static VeldridBlendFunction CreateBlendFunction(MlangBlendFunction f) => f switch
    {
        MlangBlendFunction.Add => VeldridBlendFunction.Add,
        MlangBlendFunction.Maximum => VeldridBlendFunction.Maximum,
        MlangBlendFunction.Minimum => VeldridBlendFunction.Minimum,
        MlangBlendFunction.ReverseSubtract => VeldridBlendFunction.ReverseSubtract,
        MlangBlendFunction.Subtract => VeldridBlendFunction.Subtract,
        _ => throw new NotImplementedException($"Unimplemented Mlang blend function: {f}")
    };

    private static DepthStencilStateDescription CreateDepthStencilState(PipelineState state) => new()
    {
        DepthComparison = CreateComparisonKind(state.DepthComparison),
        DepthTestEnabled = state.DepthTest,
        DepthWriteEnabled = state.DepthWrite,
        StencilTestEnabled = state.StencilTest,
        StencilReadMask = state.StencilReadMask,
        StencilWriteMask = state.StencilWriteMask,
        StencilReference = state.StencilReference,
        StencilBack = CreateStencilBehavior(state.StencilBack),
        StencilFront = CreateStencilBehavior(state.StencilFront),
    };

    private static StencilBehaviorDescription CreateStencilBehavior(StencilState state) => new()
    {
        Comparison = CreateComparisonKind(state.Comparison),
        Pass = CreateStencilOperation(state.Pass),
        Fail = CreateStencilOperation(state.Fail),
        DepthFail = CreateStencilOperation(state.DepthFail),
    };

    private static VeldridComparisonKind CreateComparisonKind(MlangComparisonKind kind) => kind switch
    {
        MlangComparisonKind.Always => VeldridComparisonKind.Always,
        MlangComparisonKind.Equal => VeldridComparisonKind.Equal,
        MlangComparisonKind.Greater => VeldridComparisonKind.Greater,
        MlangComparisonKind.GreaterEqual => VeldridComparisonKind.GreaterEqual,
        MlangComparisonKind.Less => VeldridComparisonKind.Less,
        MlangComparisonKind.LessEqual => VeldridComparisonKind.LessEqual,
        MlangComparisonKind.Never => VeldridComparisonKind.Never,
        MlangComparisonKind.NotEqual => VeldridComparisonKind.NotEqual,
        _ => throw new NotImplementedException($"Unimplemented Mlang comparison kind: {kind}")
    };

    private static VeldridStencilOperation CreateStencilOperation(MlangStencilOperation op) => op switch
    {
        MlangStencilOperation.DecrementAndClamp => VeldridStencilOperation.DecrementAndClamp,
        MlangStencilOperation.DecrementAndWrap => VeldridStencilOperation.DecrementAndWrap,
        MlangStencilOperation.IncrementAndClamp => VeldridStencilOperation.IncrementAndClamp,
        MlangStencilOperation.IncrementAndWrap => VeldridStencilOperation.IncrementAndWrap,
        MlangStencilOperation.Invert => VeldridStencilOperation.Invert,
        MlangStencilOperation.Keep => VeldridStencilOperation.Keep,
        MlangStencilOperation.Replace => VeldridStencilOperation.Replace,
        MlangStencilOperation.Zero => VeldridStencilOperation.Zero,
        _ => throw new NotImplementedException($"Unimplemented Mlang stencil operation: {op}")
    };

    private static OutputDescription CreateOutputDescription(PipelineState state) => new()
    {
        ColorAttachments = state.ColorOutputs.Select(c => new OutputAttachmentDescription(CreatePixelFormat(c.Value))).ToArray(),
        DepthAttachment = state.DepthOutput == null ? null : new(global::zzre.rendering.ShaderVariantCollection.CreatePixelFormat(state.DepthOutput.Value)),
        SampleCount = CreateSampleCount(state.OutputSamples)
    };

    private static VeldridPixelFormat CreatePixelFormat(MlangPixelFormat f) => f switch
    {
        MlangPixelFormat.B8_G8_R8_A8_UNorm => VeldridPixelFormat.B8_G8_R8_A8_UNorm,
        MlangPixelFormat.B8_G8_R8_A8_UNorm_SRgb => VeldridPixelFormat.B8_G8_R8_A8_UNorm_SRgb,
        MlangPixelFormat.R10_G10_B10_A2_UInt => VeldridPixelFormat.R10_G10_B10_A2_UInt,
        MlangPixelFormat.R10_G10_B10_A2_UNorm => VeldridPixelFormat.R10_G10_B10_A2_UNorm,
        MlangPixelFormat.R11_G11_B10_Float => VeldridPixelFormat.R11_G11_B10_Float,
        MlangPixelFormat.R16_Float => VeldridPixelFormat.R16_Float,
        MlangPixelFormat.R16_G16_B16_A16_Float => VeldridPixelFormat.R16_G16_B16_A16_Float,
        MlangPixelFormat.R16_G16_B16_A16_SInt => VeldridPixelFormat.R16_G16_B16_A16_SInt,
        MlangPixelFormat.R16_G16_B16_A16_SNorm => VeldridPixelFormat.R16_G16_B16_A16_SNorm,
        MlangPixelFormat.R16_G16_B16_A16_UInt => VeldridPixelFormat.R16_G16_B16_A16_UInt,
        MlangPixelFormat.R16_G16_B16_A16_UNorm => VeldridPixelFormat.R16_G16_B16_A16_UNorm,
        MlangPixelFormat.R16_G16_Float => VeldridPixelFormat.R16_G16_Float,
        MlangPixelFormat.R16_G16_SInt => VeldridPixelFormat.R16_G16_SInt,
        MlangPixelFormat.R16_G16_SNorm => VeldridPixelFormat.R16_G16_SNorm,
        MlangPixelFormat.R16_G16_UInt => VeldridPixelFormat.R16_G16_UInt,
        MlangPixelFormat.R16_G16_UNorm => VeldridPixelFormat.R16_G16_UNorm,
        MlangPixelFormat.R16_SInt => VeldridPixelFormat.R16_SInt,
        MlangPixelFormat.R16_SNorm => VeldridPixelFormat.R16_SNorm,
        MlangPixelFormat.R16_UInt => VeldridPixelFormat.R16_UInt,
        MlangPixelFormat.R16_UNorm => VeldridPixelFormat.R16_UNorm,
        MlangPixelFormat.R32_Float => VeldridPixelFormat.R32_Float,
        MlangPixelFormat.R32_G32_B32_A32_Float => VeldridPixelFormat.R32_G32_B32_A32_Float,
        MlangPixelFormat.R32_G32_B32_A32_SInt => VeldridPixelFormat.R32_G32_B32_A32_SInt,
        MlangPixelFormat.R32_G32_B32_A32_UInt => VeldridPixelFormat.R32_G32_B32_A32_UInt,
        MlangPixelFormat.R32_G32_Float => VeldridPixelFormat.R32_G32_Float,
        MlangPixelFormat.R32_G32_SInt => VeldridPixelFormat.R32_G32_SInt,
        MlangPixelFormat.R32_G32_UInt => VeldridPixelFormat.R32_G32_UInt,
        MlangPixelFormat.R32_SInt => VeldridPixelFormat.R32_SInt,
        MlangPixelFormat.R32_UInt => VeldridPixelFormat.R32_UInt,
        MlangPixelFormat.R8_G8_B8_A8_SInt => VeldridPixelFormat.R8_G8_B8_A8_SInt,
        MlangPixelFormat.R8_G8_B8_A8_SNorm => VeldridPixelFormat.R8_G8_B8_A8_SNorm,
        MlangPixelFormat.R8_G8_B8_A8_UInt => VeldridPixelFormat.R8_G8_B8_A8_UInt,
        MlangPixelFormat.R8_G8_B8_A8_UNorm => VeldridPixelFormat.R8_G8_B8_A8_UNorm,
        MlangPixelFormat.R8_G8_B8_A8_UNorm_SRgb => VeldridPixelFormat.R8_G8_B8_A8_UNorm_SRgb,
        MlangPixelFormat.R8_G8_SInt => VeldridPixelFormat.R8_G8_SInt,
        MlangPixelFormat.R8_G8_SNorm => VeldridPixelFormat.R8_G8_SNorm,
        MlangPixelFormat.R8_G8_UInt => VeldridPixelFormat.R8_G8_UInt,
        MlangPixelFormat.R8_G8_UNorm => VeldridPixelFormat.R8_G8_UNorm,
        MlangPixelFormat.R8_SInt => VeldridPixelFormat.R8_SInt,
        MlangPixelFormat.R8_SNorm => VeldridPixelFormat.R8_SNorm,
        MlangPixelFormat.R8_UInt => VeldridPixelFormat.R8_UInt,
        MlangPixelFormat.R8_UNorm => VeldridPixelFormat.R8_UNorm,
        MlangPixelFormat.D24_UNorm_S8_UInt => VeldridPixelFormat.D24_UNorm_S8_UInt,
        MlangPixelFormat.D32_Float_S8_UInt => VeldridPixelFormat.D32_Float_S8_UInt,
        _ => throw new NotImplementedException($"Unimplemented Mlang pixel format: {f}")
    };

    private static TextureSampleCount CreateSampleCount(byte count) => count switch
    {
        1 => TextureSampleCount.Count1,
        2 => TextureSampleCount.Count2,
        4 => TextureSampleCount.Count4,
        8 => TextureSampleCount.Count8,
        16 => TextureSampleCount.Count16,
        32 => TextureSampleCount.Count32,
        _ => throw new NotSupportedException($"Unsupported sample count: {count}")
    };

    private static VeldridPrimitiveTopology CreatePrimitiveTopology(MlangPrimitiveTopology t) => t switch
    {
        MlangPrimitiveTopology.LineList => VeldridPrimitiveTopology.LineList,
        MlangPrimitiveTopology.LineStrip => VeldridPrimitiveTopology.LineStrip,
        MlangPrimitiveTopology.PointList => VeldridPrimitiveTopology.PointList,
        MlangPrimitiveTopology.TriangleList => VeldridPrimitiveTopology.TriangleList,
        MlangPrimitiveTopology.TriangleStrip => VeldridPrimitiveTopology.TriangleStrip,
        _ => throw new NotImplementedException($"Unimplemented Mlang primitive topology: {t}")
    };

    private static RasterizerStateDescription CreateRasterizerState(PipelineState state) => new()
    {
        DepthClipEnabled = state.DepthClip,
        ScissorTestEnabled = state.ScissorTest,
        FrontFace = CreateFrontFace(state.FrontFace),
        CullMode = CreateCullMode(state.CullMode),
        FillMode = CreateFillMode(state.FillMode)
    };

    private static VeldridFrontFace CreateFrontFace(MlangFrontFace f) => f switch
    {
        MlangFrontFace.Clockwise => VeldridFrontFace.Clockwise,
        MlangFrontFace.CounterClockwise => VeldridFrontFace.CounterClockwise,
        _ => throw new NotImplementedException($"Unimplemented Mlang front face: {f}")
    };

    private static VeldridCullMode CreateCullMode(MlangCullMode m) => m switch
    {
        MlangCullMode.None => VeldridCullMode.None,
        MlangCullMode.Back => VeldridCullMode.Back,
        MlangCullMode.Front => VeldridCullMode.Front,
        _ => throw new NotImplementedException($"Unimplemented Mlang cull mode: {m}")
    };

    private static VeldridFillMode CreateFillMode(MlangFillMode m) => m switch
    {
        MlangFillMode.Solid => VeldridFillMode.Solid,
        MlangFillMode.Wireframe => VeldridFillMode.Wireframe,
        _ => throw new NotImplementedException($"Unimplemented Mlang fill mode: {m}")
    };

    private static VertexLayoutDescription[] CreateVertexLayouts(ShaderVariant variant) => variant.VertexAttributes.Select(attr => new VertexLayoutDescription()
    {
        InstanceStepRate = attr.IsInstance ? 1u : 0u,
        Stride = (uint)(SizeOfScalar(attr.Type) * attr.Type.Rows * attr.Type.Columns),
        Elements = Enumerable.Range(0, attr.Type.Columns).Select(column => new VertexElementDescription()
        {
            Name = $"{attr.Name}_{column}",
            Offset = (uint)(column * SizeOfScalar(attr.Type) * attr.Type.Rows),
            Format = VertexFormats.TryGetValue(AsColumnVector(attr.Type), out var format) ? format
                : throw new NotSupportedException($"Unsupported vertex element format {attr.Type} for {attr.Name}")
        }).ToArray()
    }).ToArray();

    private static int SizeOfScalar(NumericType type) => type.ScalarWidth switch
    {
        ScalarWidth.Byte => 1,
        ScalarWidth.Word => 2,
        ScalarWidth.DWord => 4,
        _ => throw new NotImplementedException($"Unimplemented Mlang scalar width: {type.ScalarWidth}")
    };

    private static NumericType AsColumnVector(NumericType t) =>
        new(t.Scalar, 1, t.Rows, t.ScalarWidth, t.IsNormalized);

    private static readonly IReadOnlyDictionary<NumericType, VertexElementFormat> VertexFormats = new Dictionary<NumericType, VertexElementFormat>
    {
        { new(ScalarType.Float, Rows: 1), VertexElementFormat.Float1 },
        { new(ScalarType.Float, Rows: 2), VertexElementFormat.Float2 },
        { new(ScalarType.Float, Rows: 3), VertexElementFormat.Float3 },
        { new(ScalarType.Float, Rows: 4), VertexElementFormat.Float4 },
        { new(ScalarType.UInt, ScalarWidth: ScalarWidth.Byte, Rows: 2, IsNormalized: true), VertexElementFormat.Byte2_Norm },
        { new(ScalarType.UInt, ScalarWidth: ScalarWidth.Byte, Rows: 2, IsNormalized: false), VertexElementFormat.Byte2 },
        { new(ScalarType.UInt, ScalarWidth: ScalarWidth.Byte, Rows: 4, IsNormalized: true), VertexElementFormat.Byte4_Norm },
        { new(ScalarType.UInt, ScalarWidth: ScalarWidth.Byte, Rows: 4, IsNormalized: false), VertexElementFormat.Byte4 },
        { new(ScalarType.Int, ScalarWidth: ScalarWidth.Byte, Rows: 2, IsNormalized: true), VertexElementFormat.SByte2_Norm },
        { new(ScalarType.Int, ScalarWidth: ScalarWidth.Byte, Rows: 2, IsNormalized: false), VertexElementFormat.SByte2 },
        { new(ScalarType.Int, ScalarWidth: ScalarWidth.Byte, Rows: 4, IsNormalized: true), VertexElementFormat.SByte4_Norm },
        { new(ScalarType.Int, ScalarWidth: ScalarWidth.Byte, Rows: 4, IsNormalized: false), VertexElementFormat.SByte4 },
        { new(ScalarType.UInt, ScalarWidth: ScalarWidth.Word, Rows: 2, IsNormalized: true), VertexElementFormat.UShort2_Norm },
        { new(ScalarType.UInt, ScalarWidth: ScalarWidth.Word, Rows: 2, IsNormalized: false), VertexElementFormat.UShort2 },
        { new(ScalarType.UInt, ScalarWidth: ScalarWidth.Word, Rows: 4, IsNormalized: true), VertexElementFormat.UShort4_Norm },
        { new(ScalarType.UInt, ScalarWidth: ScalarWidth.Word, Rows: 4, IsNormalized: false), VertexElementFormat.UShort4 },
        { new(ScalarType.Int, ScalarWidth: ScalarWidth.Word, Rows: 2, IsNormalized: true), VertexElementFormat.Short2_Norm },
        { new(ScalarType.Int, ScalarWidth: ScalarWidth.Word, Rows: 2, IsNormalized: false), VertexElementFormat.Short2 },
        { new(ScalarType.Int, ScalarWidth: ScalarWidth.Word, Rows: 4, IsNormalized: true), VertexElementFormat.Short4_Norm },
        { new(ScalarType.Int, ScalarWidth: ScalarWidth.Word, Rows: 4, IsNormalized: false), VertexElementFormat.Short4 },
        { new(ScalarType.UInt, Rows: 1), VertexElementFormat.UInt1 },
        { new(ScalarType.UInt, Rows: 2), VertexElementFormat.UInt2 },
        { new(ScalarType.UInt, Rows: 3), VertexElementFormat.UInt3 },
        { new(ScalarType.UInt, Rows: 4), VertexElementFormat.UInt4 },
        { new(ScalarType.Int, Rows: 1), VertexElementFormat.Int1 },
        { new(ScalarType.Int, Rows: 2), VertexElementFormat.Int2 },
        { new(ScalarType.Int, Rows: 3), VertexElementFormat.Int3 },
        { new(ScalarType.Int, Rows: 4), VertexElementFormat.Int4 }
    };
}
