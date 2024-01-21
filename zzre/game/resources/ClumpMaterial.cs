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

public readonly record struct ClumpMaterialInfo(FOModelRenderType RenderType, RWMaterial RWMaterial);

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
            if (info.RenderType is FOModelRenderType.LateAdditive)
                material.DepthWrite = false;
        }
        else if (info.RenderType is >= FOModelRenderType.EnvMap32 and <= FOModelRenderType.EnvMap255)
        {
            // TODO: EnvMap materials do not use ZBias
            material.Blend = ModelMaterial.BlendMode.Alpha;
            material.HasEnvMap = true;
            material.DepthWrite = false;
        }
        else
            throw new NotSupportedException($"Unsupported render type for material {info.RenderType}");

        (material.Texture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(TextureBasePaths, info.RWMaterial);
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        material.Factors.Ref = new()
        {
            vertexColorFactor = 0f,
            tintFactor = 1f,
            alphaReference = 0.082352944f
        };
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
