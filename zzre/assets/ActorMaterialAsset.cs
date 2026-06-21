using System;
using System.Threading;
using System.Threading.Tasks;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzre.materials;
using zzre.rendering;
using static zzre.ActorMaterialAsset;

namespace zzre;

public sealed class ActorMaterialAsset(IAssetRegistry registry) : IAsset<Info>, ITexturedMaterialAsset
{
    private static readonly FilePath[] ActorTextureBasePaths =
    [
        new FilePath("resources/textures/actorsex"),
        new FilePath("resources/textures/models"),
        new FilePath("resources/textures/worlds"),
    ];

    // no factors, they are managed by ActorRenderer to set ambient light

    private static readonly ModelMaterial.Variant MaterialVariant = new(
        IsInstanced: false,
        IsSkinned: true, // TODO: comment why generally yes, but can differ
        HasTexShift: false);

    static AssetLocality IAsset.Locality => AssetLocality.Unique; // due to skeleton buffers, no reuse possible
    static bool IAsset.NeedsMainThreadDisposal => true; // an Apply action wants to access Material

    public readonly record struct Info(
        string? TextureName,
        SamplerDescription Sampler,
        IColor Color,
        bool IsSkinned);

    private AssetHandle<TextureAsset> textureHandle;
    private AssetHandle<SamplerAsset> samplerHandle;

    public IAssetRegistry Registry { get; } = registry;
    public ModelMaterial Material { get; private set; } = null!;
    ITexturedMaterial ITexturedMaterialAsset.Material => Material;

    static async Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Guid assetId, Info info, CancellationToken ct)
    {
        var diContainer = registry.DIContainer;
        var material = new ModelMaterial(diContainer)
        {
            DebugName = $"ActorMat {info.TextureName} {(info.IsSkinned ? "" : "Unskinned")}"
        };
        material.Apply(
            MaterialVariant with { IsSkinned = info.IsSkinned },
            factors: null,
            diContainer
        );
        material.Tint.Ref = info.Color;

        var camera = diContainer.GetTag<Camera>();
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        var samplerHandle = registry.LoadSampler(info.Sampler, AssetPriority.High);
        material.Sampler.Sampler = (await samplerHandle.GetAsync(ct)).Sampler;
        var (initialTexture, textureHandle) = await AssetExtensions.LoadTextureForMaterial(
            registry,
            ActorTextureBasePaths,
            assetId,
            info.TextureName,
            StandardTextureKind.White,
            AssetPriority.High,
            ct);
        material.Texture.Texture = initialTexture;

        return new(new ActorMaterialAsset(registry)
        {
            samplerHandle = samplerHandle,
            textureHandle = textureHandle,
            Material = material
        });
    }

    public void Dispose()
    {
        textureHandle.Dispose();
        samplerHandle.Dispose();
        Material?.Dispose();
        Material = null!;
    }
}

partial class AssetExtensions
{
    public static AssetHandle<ActorMaterialAsset> LoadActorMaterial(this IAssetRegistry registry,
        string? textureName,
        SamplerDescription sampler,
        IColor color,
        bool isSkinned = true,
        AssetPriority priority = AssetPriority.Synchronous) =>
        registry.Load<Info, ActorMaterialAsset>(
            new(textureName, sampler, color, isSkinned),
            priority);

    public static AssetHandle<ActorMaterialAsset> LoadActorMaterial(this IAssetRegistry registry,
        RWMaterial rwMaterial,
        bool isSkinned = true,
        AssetPriority priority = AssetPriority.Synchronous)
    {
        var rwTexture = rwMaterial.FindChildById(SectionId.Texture, true) as RWTexture;
        var rwTextureName = (rwTexture?.FindChildById(SectionId.String, true) as RWString)?.value;
        var samplerDescription = GetSamplerDescription(rwTexture);
        return registry.LoadActorMaterial(
            rwTextureName,
            samplerDescription,
            rwMaterial.color,
            isSkinned,
            priority);
    }
}
