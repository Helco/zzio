using System;
using System.Threading;
using System.Threading.Tasks;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzre.materials;
using zzre.rendering;

namespace zzre;

public sealed class WorldMaterialAsset(IAssetRegistry registry) : IAsset<WorldMaterialAsset.Info>, ITexturedMaterialAsset
{
    private static readonly FilePath[] WorldTextureBasePaths =
    [
        new FilePath("resources/textures/worlds")
    ];

    private static readonly ModelMaterial.Variant MaterialVariant = new(
        IsInstanced: false,
        HasTexShift: false,
        HasFog: true
    );

    private static readonly ModelFactors ModelFactors = new()
    {
        textureFactor = 1f,
        vertexColorFactor = 1f,
        tintFactor = 1f,
        alphaReference = 0.6f
    };

    static AssetLocality IAsset.Locality => AssetLocality.Local;
    static bool IAsset.NeedsMainThreadDisposal => true; // an Apply action wants to access Material

    public readonly record struct Info(
        string? TextureName,
        SamplerDescription Sampler);

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
            DebugName = $"WorldMat {info.TextureName}"
        };
        material.Apply(MaterialVariant, ModelFactors, diContainer);

        var camera = diContainer.GetTag<Camera>();
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        var samplerHandle = registry.LoadSampler(info.Sampler, AssetPriority.High);
        material.Sampler.Sampler = (await samplerHandle.GetAsync(ct)).Sampler;
        var (initialTexture, textureHandle) = await AssetExtensions.LoadTextureForMaterial(
            registry,
            WorldTextureBasePaths,
            assetId,
            info.TextureName,
            StandardTextureKind.White,
            AssetPriority.High,
            ct);
        material.Texture.Texture = initialTexture;

        return new(new WorldMaterialAsset(registry)
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

    public override string ToString() => Material?.DebugName ?? "Disposed WorldMaterial";
}

partial class AssetExtensions
{
    public static AssetHandle<WorldMaterialAsset> LoadWorldMaterial(this IAssetRegistry registry,
        string? textureName,
        SamplerDescription sampler,
        AssetPriority priority = AssetPriority.Synchronous) =>
        registry.Load<WorldMaterialAsset.Info, WorldMaterialAsset>(new(textureName, sampler), priority);

    public static AssetHandle<WorldMaterialAsset> LoadWorldMaterial(this IAssetRegistry registry,
        RWMaterial rwMaterial,
        AssetPriority priority = AssetPriority.Synchronous)
    {
        var rwTexture = rwMaterial.FindChildById(SectionId.Texture, true) as RWTexture;
        var rwTextureName = (rwTexture?.FindChildById(SectionId.String, true) as RWString)?.value;
        var samplerDescription = GetSamplerDescription(rwTexture);
        return registry.LoadWorldMaterial(rwTextureName, samplerDescription, priority);
    }
}
