using System;
using DefaultEcs;
using DefaultEcs.Resource;
using zzio;

namespace zzre.game.resources;

public enum ClumpType
{
    Model,
    Actor,
    Backdrop
}

public readonly struct ClumpInfo : IEquatable<ClumpInfo>
{
    private static readonly FilePath BasePath = new("resources/models/");

    public readonly ClumpType Type;
    public readonly string Name;

    public ClumpInfo(ClumpType type, string name)
    {
        Type = type;
        Name = name;
    }

    public static ClumpInfo Model(string name) => new(ClumpType.Model, name);
    public static ClumpInfo Actor(string name) => new(ClumpType.Actor, name);
    public static ClumpInfo Backdrop(string name) => new(ClumpType.Backdrop, name);

    public override bool Equals(object? obj) => obj is ClumpInfo info && Equals(info);
    public bool Equals(ClumpInfo other) => Type == other.Type && Name == other.Name;
    public static bool operator ==(ClumpInfo left, ClumpInfo right) => left.Equals(right);
    public static bool operator !=(ClumpInfo left, ClumpInfo right) => !(left == right);
    public override int GetHashCode() => HashCode.Combine(Type, Name);

    public FilePath Path => BasePath.Combine(
        Type switch
        {
            ClumpType.Model => "models",
            ClumpType.Actor => "actorsex",
            ClumpType.Backdrop => "backdrops",
            _ => throw new NotSupportedException($"Unsupported clump type {Type}")
        }, Name);
}

public class Clump : AResourceManager<ClumpInfo, ClumpBuffers>
{
    private readonly ITagContainer diContainer;

    public Clump(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override ClumpBuffers Load(ClumpInfo info) => new(diContainer, info.Path);

    protected override void OnResourceLoaded(in Entity entity, ClumpInfo info, ClumpBuffers resource)
    {
        entity.Set(info);
        entity.Set(resource);
        if (resource.Skin != null)
            entity.Set(new Skeleton(resource.Skin, info.Name.Replace(".DFF", "", StringComparison.InvariantCultureIgnoreCase)));
    }
}
