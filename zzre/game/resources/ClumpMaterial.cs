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

public readonly struct ClumpMaterialInfo : IEquatable<ClumpMaterialInfo>
{
    public readonly FOModelRenderType RenderType;
    public readonly RWMaterial RWMaterial;

    public ClumpMaterialInfo(FOModelRenderType renderType, RWMaterial rwMaterial) =>
        (RenderType, RWMaterial) = (renderType, rwMaterial);

    public bool Equals(ClumpMaterialInfo other) =>
        RenderType == other.RenderType &&
        ReferenceEquals(RWMaterial, other.RWMaterial);
    public override bool Equals(object? obj) => obj is ClumpMaterialInfo info && Equals(info);
    public override int GetHashCode() => HashCode.Combine(RenderType, RWMaterial);
    public static bool operator ==(ClumpMaterialInfo left, ClumpMaterialInfo right) => left.Equals(right);
    public static bool operator !=(ClumpMaterialInfo left, ClumpMaterialInfo right) => !(left == right);
}

public class ClumpMaterial : AResourceManager<ClumpMaterialInfo, BaseModelInstancedMaterial>
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

    protected override BaseModelInstancedMaterial Load(ClumpMaterialInfo info)
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

    protected override void OnResourceLoaded(in DefaultEcs.Entity entity, ClumpMaterialInfo info, BaseModelInstancedMaterial resource)
    {
        var materialList = entity.Get<List<BaseModelInstancedMaterial>>();
        materialList.Add(resource);
    }

    protected override void Unload(ClumpMaterialInfo info, BaseModelInstancedMaterial resource)
    {
        if (textureLoader is not CachedAssetLoader<Texture>)
            resource.MainTexture.Texture?.Dispose();
        resource.Dispose();
    }
}
