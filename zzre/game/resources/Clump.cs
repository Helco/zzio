using System;
using DefaultEcs;
using DefaultEcs.Resource;
using zzio;
using zzre.rendering;

namespace zzre.game.resources;

public enum ClumpType
{
    Model,
    Actor,
    Backdrop
}

public readonly record struct ClumpInfo(ClumpType Type, string Name)
{
    private static readonly FilePath BasePath = new("resources/models/");

    public static ClumpInfo Model(string name) => new(ClumpType.Model, name);
    public static ClumpInfo Actor(string name) => new(ClumpType.Actor, name);
    public static ClumpInfo Backdrop(string name) => new(ClumpType.Backdrop, name);

    public FilePath Path => BasePath.Combine(
        Type switch
        {
            ClumpType.Model => "models",
            ClumpType.Actor => "actorsex",
            ClumpType.Backdrop => "backdrops",
            _ => throw new NotSupportedException($"Unsupported clump type {Type}")
        }, Name);
}

public class Clump : AResourceManager<ClumpInfo, ClumpMesh>
{
    private readonly ITagContainer diContainer;

    public Clump(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override ClumpMesh Load(ClumpInfo info) => new(diContainer, info.Path);

    protected override void OnResourceLoaded(in Entity entity, ClumpInfo info, ClumpMesh resource)
    {
        entity.Set(info);
        entity.Set(resource);
        if (resource.Skin != null)
            entity.Set(new Skeleton(resource.Skin, info.Name.Replace(".DFF", "", StringComparison.InvariantCultureIgnoreCase)));
    }
}

