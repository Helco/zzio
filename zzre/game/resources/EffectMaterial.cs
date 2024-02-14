using System;
using DefaultEcs.Resource;
using Veldrid;
using zzio;
using zzio.effect;
using zzre.materials;
using zzre.rendering;
using static zzre.materials.EffectMaterial;

namespace zzre.game.resources;

public readonly record struct EffectMaterialInfo(
    bool DepthTest,
    BillboardMode BillboardMode,
    BlendMode BlendMode,
    string TextureName,
    float AlphaReference = 0.03f,
    bool HasFog = true)
{
    public EffectMaterialInfo(
        bool depthTest,
        BillboardMode billboardMode,
        EffectPartRenderMode renderMode,
        string textureName)
        : this(depthTest, billboardMode, RenderToBlendMode(renderMode), textureName)
    { }

    private static BlendMode RenderToBlendMode(EffectPartRenderMode renderMode) => renderMode switch
    {
        EffectPartRenderMode.Additive => BlendMode.Additive,
        EffectPartRenderMode.AdditiveAlpha => BlendMode.AdditiveAlpha,
        EffectPartRenderMode.NormalBlend => BlendMode.Alpha,
        _ => throw new NotSupportedException($"Unsupported effect part render mode: {renderMode}")
    };
}

public class EffectMaterial : AResourceManager<EffectMaterialInfo, materials.EffectMaterial>
{
    private static readonly FilePath[] TextureBasePaths =
    [
        new("resources/textures/effects"),
        new("resources/textures/models")
    ];

    private readonly ITagContainer diContainer;
    private readonly GraphicsDevice graphicsDevice;
    private readonly Camera camera;
    private readonly IAssetLoader<Texture> textureLoader;

    public EffectMaterial(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        camera = diContainer.GetTag<Camera>();
        textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override materials.EffectMaterial Load(EffectMaterialInfo info)
    {
        diContainer.TryGetTag<UniformBuffer<FogParams>>(out var fogParams);
        var material = new materials.EffectMaterial(diContainer)
        {
            DepthTest = info.DepthTest,
            Billboard = info.BillboardMode,
            Blend = info.BlendMode,
            HasFog = info.HasFog && fogParams != null,
        };
        material.Texture.Texture = textureLoader.LoadTexture(TextureBasePaths, info.TextureName);
        material.Sampler.Sampler = graphicsDevice.LinearSampler;
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        material.Factors.Value = new()
        {
            alphaReference = info.AlphaReference
        };
        if (info.HasFog && fogParams != null)
            material.FogParams.Buffer = fogParams.Buffer;
        material.DebugName = $"{info.TextureName} {info.BillboardMode} {info.BlendMode}";
        if (!info.DepthTest)
            material.DebugName += " NoDepthTest";
        return material;
    }

    protected override void Unload(EffectMaterialInfo info, materials.EffectMaterial resource)
    {
        if (textureLoader is not CachedAssetLoader<Texture>)
            resource.Texture.Texture?.Dispose();
        resource.Dispose();
    }

    protected override void OnResourceLoaded(in DefaultEcs.Entity entity, EffectMaterialInfo info, materials.EffectMaterial resource)
    {
        entity.Set(resource);
    }
}
