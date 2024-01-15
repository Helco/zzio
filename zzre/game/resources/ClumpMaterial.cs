using System;
using System.Collections.Generic;
using DefaultEcs.Resource;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzio.scn;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.resources;

public readonly record struct ClumpMaterialInfoLEGACY(FOModelRenderType RenderType, RWMaterial RWMaterial);
public readonly record struct ClumpMaterialInfo(FOModelRenderType RenderType, RWMaterial RWMaterial);

public class ClumpMaterialLEGACY : AResourceManager<ClumpMaterialInfoLEGACY, BaseModelInstancedMaterial>
{
    private static readonly FilePath[] TextureBasePaths =
    {
        new FilePath("resources/textures/models"),
        new FilePath("resources/textures/worlds")
    };

    private readonly ITagContainer diContainer;
    private readonly Camera camera;
    private readonly IAssetLoader<Texture> textureLoader;

    public ClumpMaterialLEGACY(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        camera = diContainer.GetTag<Camera>();
        textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override BaseModelInstancedMaterial Load(ClumpMaterialInfoLEGACY info)
    {
        BaseModelInstancedMaterial material = info.RenderType switch
        {
            FOModelRenderType.EarlySolid => new ModelInstancedBlendMaterial(diContainer, depthWrite: true),
            FOModelRenderType.LateSolid => new ModelInstancedBlendMaterial(diContainer, depthWrite: true),
            FOModelRenderType.Solid => new ModelInstancedBlendMaterial(diContainer, depthWrite: true),

            FOModelRenderType.LateAdditive => new ModelInstancedAdditiveAlphaMaterial(diContainer, depthWrite: false),
            FOModelRenderType.EarlyAdditive => new ModelInstancedAdditiveAlphaMaterial(diContainer, depthWrite: true),
            FOModelRenderType.Additive => new ModelInstancedAdditiveAlphaMaterial(diContainer, depthWrite: true),

            var _ => throw new NotSupportedException($"Unsupported render type for material {info.RenderType}")
        };

        (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(TextureBasePaths, info.RWMaterial);
        material.Uniforms.Ref.vertexColorFactor = 0.0f;
        material.Uniforms.Ref.alphaReference = 0.082352944f;
        material.Uniforms.Ref.tintFactor = 1f;
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        return material;
    }

    protected override void OnResourceLoaded(in DefaultEcs.Entity entity, ClumpMaterialInfoLEGACY info, BaseModelInstancedMaterial resource)
    {
        var materialList = entity.Get<List<BaseModelInstancedMaterial>>();
        materialList.Add(resource);
    }

    protected override void Unload(ClumpMaterialInfoLEGACY info, BaseModelInstancedMaterial resource)
    {
        if (textureLoader is not CachedAssetLoader<Texture>)
            resource.MainTexture.Texture?.Dispose();
        resource.Dispose();
    }
}

public class ClumpMaterial : AResourceManager<ClumpMaterialInfo, ModelMaterial>
{
    private static readonly FilePath[] TextureBasePaths =
    {
        new FilePath("resources/textures/models"),
        new FilePath("resources/textures/worlds")
    };

    private readonly ITagContainer diContainer;
    private readonly Camera camera;
    private readonly IAssetLoader<Texture> textureLoader;

    public ClumpMaterial(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        camera = diContainer.GetTag<Camera>();
        textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override ModelMaterial Load(ClumpMaterialInfo info)
    {
        var material = new ModelMaterial(diContainer) { HasTexShift = true, IsInstanced = true };
        if (info.RenderType is FOModelRenderType.EarlySolid or FOModelRenderType.LateSolid or FOModelRenderType.Solid)
            material.Blend = ModelMaterial.BlendMode.Alpha;
        else if (info.RenderType is FOModelRenderType.LateAdditive or FOModelRenderType.EarlyAdditive or FOModelRenderType.Additive)
        {
            material.Blend = ModelMaterial.BlendMode.AdditiveAlpha;
            // TODO: Fix LateAdditive writing to depth buffer
        }
        else
            throw new NotSupportedException($"Unsupported render type for material {info.RenderType}");

        (material.Texture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(TextureBasePaths, info.RWMaterial);
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        return material;
    }

    protected override void OnResourceLoaded(in DefaultEcs.Entity entity, ClumpMaterialInfo info, ModelMaterial resource)
    {
        var materialList = entity.Get<List<ModelMaterial>>();
        materialList.Add(resource);
    }

    protected override void Unload(ClumpMaterialInfo info, ModelMaterial resource)
    {
        if (textureLoader is not CachedAssetLoader<Texture>)
            resource.Texture.Texture?.Dispose();
        resource.Dispose();
    }
}
