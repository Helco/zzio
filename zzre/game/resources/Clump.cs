using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs;
using DefaultEcs.Resource;
using zzio.utils;
using zzio.vfs;

namespace zzre.game.resources
{
    public enum ClumpType
    {
        Model,
        Actor,
        Backdrop
    }

    public readonly struct ClumpInfo : IEquatable<ClumpInfo>
    {
        private static readonly FilePath BasePath = new FilePath("resources/models/");
        private static readonly string FileExtension = ".dff";

        public readonly ClumpType Type;
        public readonly string Name;

        public ClumpInfo(ClumpType type, string name)
        {
            Type = type;
            Name = name;
        }

        public static ClumpInfo Model(string name) => new ClumpInfo(ClumpType.Model, name);
        public static ClumpInfo Actor(string name) => new ClumpInfo(ClumpType.Actor, name);
        public static ClumpInfo Backdrop(string name) => new ClumpInfo(ClumpType.Backdrop, name);

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
            }, Name.EndsWith(FileExtension) ? Name : Name + FileExtension);
    }

    public class Clump : AResourceManager<ClumpInfo, ClumpBuffers>
    {
        private readonly ITagContainer diContainer;

        public Clump(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            Manage(diContainer.GetTag<DefaultEcs.World>());
        }

        protected override ClumpBuffers Load(ClumpInfo info) => new ClumpBuffers(diContainer, info.Path);

        protected override void OnResourceLoaded(in Entity entity, ClumpInfo info, ClumpBuffers resource)
        {
            entity.Set(resource);
            if (resource.Skin != null)
                entity.Set(new Skeleton(resource.Skin));
        }
    }
}
