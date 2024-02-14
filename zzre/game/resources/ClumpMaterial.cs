﻿using System;
using System.Collections.Generic;
using DefaultEcs.Resource;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzio.scn;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.resources;

public readonly record struct ClumpMaterialInfo(
    RWMaterial RWMaterial,
    ModelMaterial.BlendMode BlendMode = ModelMaterial.BlendMode.Opaque,
    bool DepthWrite = true,
    bool DepthTest = true,
    bool HasEnvMap = false,
    bool HasTexShift = true,
    bool HasFog = true)
{
    public ClumpMaterialInfo(FOModelRenderType? renderType, RWMaterial rwMaterial) : this(rwMaterial)
    {
        switch(renderType)
        {
            case null:
                BlendMode = ModelMaterial.BlendMode.Opaque;
                break;
            case FOModelRenderType.EarlySolid:
            case FOModelRenderType.LateSolid:
            case FOModelRenderType.Solid:
                BlendMode = ModelMaterial.BlendMode.Alpha;
                break;
            case FOModelRenderType.EarlyAdditive:
            case FOModelRenderType.Additive:
                BlendMode = ModelMaterial.BlendMode.AdditiveAlpha;
                HasFog = false;
                break;
            case FOModelRenderType.LateAdditive:
                BlendMode = ModelMaterial.BlendMode.AdditiveAlpha;
                DepthWrite = false;
                HasFog = false;
                break;
            case FOModelRenderType.EnvMap32:
            case FOModelRenderType.EnvMap64:
            case FOModelRenderType.EnvMap96:
            case FOModelRenderType.EnvMap128:
            case FOModelRenderType.EnvMap196:
            case FOModelRenderType.EnvMap255:
                BlendMode = ModelMaterial.BlendMode.Alpha;
                HasEnvMap = true;
                DepthWrite = false;
                break;
            default:
                throw new NotSupportedException($"Unsupported render type for material {renderType}");
        }
    }

    public ClumpMaterialInfo(RWMaterial rwMaterial, zzio.effect.EffectPartRenderMode renderMode, bool depthTest)
        : this(rwMaterial, renderMode switch
        {
            zzio.effect.EffectPartRenderMode.Additive => ModelMaterial.BlendMode.Additive,
            zzio.effect.EffectPartRenderMode.AdditiveAlpha => ModelMaterial.BlendMode.AdditiveAlpha,
            zzio.effect.EffectPartRenderMode.NormalBlend => ModelMaterial.BlendMode.Alpha,
            _ => throw new NotSupportedException($"Unsupported effect part render mode: {renderMode}")
        }, DepthWrite: false, depthTest, HasTexShift: false)
    { }
}

public class ClumpMaterial : AResourceManager<ClumpMaterialInfo, ModelMaterial>
{
    private static readonly FilePath[] TextureBasePaths =
    [
        new FilePath("resources/textures/models"),
        new FilePath("resources/textures/worlds"),
        new FilePath("resources/textures/backdrops"),
    ];

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
        var material = new ModelMaterial(diContainer)
        {
            IsInstanced = true,
            Blend = info.BlendMode,
            DepthTest = info.DepthTest,
            DepthWrite = info.DepthWrite,
            HasEnvMap = info.HasEnvMap,
            HasTexShift = info.HasTexShift,
            HasFog = info.HasFog
        };

        (material.Texture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(TextureBasePaths, info.RWMaterial);
        material.Projection.BufferRange = camera.ProjectionRange;
        material.View.BufferRange = camera.ViewRange;
        material.Factors.Ref = new()
        {
            textureFactor = 1f,
            vertexColorFactor = 1f,
            tintFactor = 1f,
            alphaReference = 0.082352944f
        };
        // only a little bit hacky: we delay retrieving the fog params for the system to initialize
        if (info.HasFog && diContainer.TryGetTag<UniformBuffer<FogParams>>(out var fogParams))
            material.FogParams.Buffer = fogParams.Buffer;
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
