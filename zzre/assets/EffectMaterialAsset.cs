using System;
using System.Collections.Generic;
using Veldrid;
using zzio;
using zzio.effect;
using zzre.materials;
using zzre.rendering;
using static zzre.materials.EffectMaterial;

namespace zzre;

public sealed class EffectMaterialAsset : Asset
{
    private static readonly FilePath[] TextureBasePaths =
    [
        new("resources/textures/effects"),
        new("resources/textures/models")
    ];

    public readonly record struct Info(
        string TextureName,
        BillboardMode BillboardMode,
        BlendMode BlendMode,
        bool DepthTest,
        bool HasFog = true,
        float AlphaReference = 0.03f,
        StandardTextureKind TexturePlaceholder = StandardTextureKind.Clear);

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<EffectMaterialAsset>(AssetLocality.Context);

    private readonly Info info;
    private EffectMaterial? material;

    public string DebugName { get; }
    public EffectMaterial Material => material ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public EffectMaterialAsset(IAssetRegistry registry, Guid assetId, Info info) : base(registry, assetId)
    {
        this.info = info;
        DebugName = $"{info.TextureName} {info.BillboardMode} {info.BlendMode}";
        if (!info.DepthTest)
            DebugName += " NoDepthTest";
    }

    protected override bool NeedsSecondaryAssets => false;

    protected override IEnumerable<AssetHandle> Load()
    {
        diContainer.TryGetTag(out UniformBuffer<FogParams> fogParams);
        material = new EffectMaterial(diContainer)
        {
            DepthTest = info.DepthTest,
            Billboard = info.BillboardMode,
            Blend = info.BlendMode,
            HasFog = info.HasFog && fogParams != null,
            DebugName = DebugName
        };

        var camera = diContainer.GetTag<Camera>();
        var samplerHandle = Registry.LoadSampler(SamplerDescription.Linear);
        var textureHandle = LoadTexture();
        material.Sampler.Sampler = samplerHandle.Get().Sampler;
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        material.Factors.Value = new()
        {
            alphaReference = info.AlphaReference
        };
        if (info.HasFog && fogParams is not null)
            material.FogParams.Buffer = fogParams.Buffer;
        
        return textureHandle is null
            ? [ samplerHandle ]
            : [ samplerHandle, textureHandle.Value ];
    }

    private AssetHandle? LoadTexture()
    {
        if (material is null)
            return null;
        var standardTextures = diContainer.GetTag<StandardTextures>();
        var handle = Registry.LoadTexture(
            TextureBasePaths,
            info.TextureName,
            AssetLoadPriority.Low,
            material);
        material.Texture.Texture ??= standardTextures.ByKind(info.TexturePlaceholder);
        return handle;
    }

    protected override void Unload()
    {
        material?.Dispose();
        material = null;
    }

    protected override string ToStringInner() => $"EffectMaterial {DebugName}";
}

partial class AssetExtensions
{
    public static AssetHandle<EffectMaterialAsset> LoadEffectMaterial(this IAssetRegistry registry,
        DefaultEcs.Entity entity,
        string textureName,
        BillboardMode billboardMode,
        EffectPartRenderMode renderMode,
        bool depthTest) =>
        registry.LoadEffectMaterial(entity, textureName, billboardMode, RenderToBlendMode(renderMode), depthTest);

    public static unsafe AssetHandle<EffectMaterialAsset> LoadEffectMaterial(this IAssetRegistry registry,
        DefaultEcs.Entity entity,
        string TextureName,
        BillboardMode BillboardMode,
        BlendMode BlendMode,
        bool DepthTest,
        bool HasFog = true,
        float AlphaReference = 0.03f,
        StandardTextureKind TexturePlaceholder = StandardTextureKind.Clear) =>
        registry.LoadEffectMaterial(entity, new EffectMaterialAsset.Info(
            TextureName, BillboardMode, BlendMode, DepthTest, HasFog, AlphaReference, TexturePlaceholder));

    public static unsafe AssetHandle<EffectMaterialAsset> LoadEffectMaterial(this IAssetRegistry registry,
        DefaultEcs.Entity entity,
        in EffectMaterialAsset.Info info)
    {
        var handle = registry.Load(info, AssetLoadPriority.Synchronous, &ApplyEffectMaterialToEntity, entity);
        entity.Set(handle);
        return handle.As<EffectMaterialAsset>();
    }

    private static void ApplyEffectMaterialToEntity(AssetHandle handle, ref readonly DefaultEcs.Entity entity)
    {
        if (entity.IsAlive)
            entity.Set(handle.Get<EffectMaterialAsset>().Material);
    }

    private static BlendMode RenderToBlendMode(EffectPartRenderMode renderMode) => renderMode switch
    {
        EffectPartRenderMode.Additive => BlendMode.Additive,
        EffectPartRenderMode.AdditiveAlpha => BlendMode.AdditiveAlpha,
        EffectPartRenderMode.NormalBlend => BlendMode.Alpha,
        _ => throw new NotSupportedException($"Unsupported effect part render mode: {renderMode}")
    };
}
