using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre;

public sealed class ClumpMaterialAsset : Asset
{
    private static readonly FilePath[] TextureBasePaths =
    [
        new FilePath("resources/textures/models"),
        new FilePath("resources/textures/worlds"),
        new FilePath("resources/textures/backdrops"),
    ];

    public readonly record struct Info(
        string textureName,
        SamplerDescription sampler,
        MaterialVariant config,
        StandardTextureKind? texturePlaceholder = null);

    public readonly record struct MaterialVariant(
        ModelMaterial.BlendMode BlendMode = ModelMaterial.BlendMode.Opaque,
        bool IsSkinned = false,
        bool DepthWrite = true,
        bool DepthTest = true,
        bool HasEnvMap = false,
        bool HasTexShift = true,
        bool HasFog = true);

    public static void Register() =>
        AssetInfoRegistry<Info>.RegisterLocal<ClumpMaterialAsset>();

    private readonly Info info;
    private ModelMaterial? material;

    public ModelMaterial Material => material ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public ClumpMaterialAsset(ITagContainer diContainer, Guid assetId, Info info) : base(diContainer, assetId)
    {
        this.info = info;
    }

    protected override bool NeedsSecondaryAssets => false;

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        material = new ModelMaterial(diContainer)
        {
            IsInstanced = true,
            IsSkinned = info.config.IsSkinned,
            Blend = info.config.BlendMode,
            DepthWrite = info.config.DepthWrite,
            DepthTest = info.config.DepthTest,
            HasEnvMap = info.config.HasEnvMap,
            HasTexShift = info.config.HasTexShift,
            HasFog = info.config.HasFog,
        };

        var camera = diContainer.GetTag<Camera>();
        var samplerHandle = Registry.LoadSampler(info.sampler);
        AssetHandle textureHandle;
        if (info.texturePlaceholder == null)
        {
            textureHandle = Registry.LoadTexture(TextureBasePaths, info.textureName, AssetLoadPriority.Synchronous);
            material.Texture.Texture = textureHandle.Get<TextureAsset>().Texture;
        }
        else
        {
            material.Texture.Texture = diContainer.GetTag<StandardTextures>().ByKind(info.texturePlaceholder.Value);
            textureHandle = Registry.LoadTexture(TextureBasePaths, info.textureName, AssetLoadPriority.High, material);
        }
        material.Sampler.Sampler = samplerHandle.Get().Sampler;
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        material.Factors.Ref = new()
        {
            textureFactor = 1f,
            vertexColorFactor = 1f,
            tintFactor = 1f,
            alphaReference = 0.082352944f
        };
        if (info.config.HasFog && diContainer.TryGetTag<UniformBuffer<FogParams>>(out var fogParams))
            material.FogParams.Buffer = fogParams.Buffer;
        return ValueTask.FromResult<IEnumerable<AssetHandle>>([ samplerHandle, textureHandle ]);
    }

    protected override void Unload()
    {
        material?.Dispose();
        material = null;
    }
}
