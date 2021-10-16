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

    public readonly struct ClumpInfo
    {
        private static readonly FilePath BasePath = new FilePath("resources/models/");
        private static readonly string FileExtension = ".dff";

        public readonly ClumpType Type;
        public readonly string name;

        public FilePath Path => BasePath.Combine(
            Type switch
            {
                ClumpType.Model => "models",
                ClumpType.Actor => "actorsex",
                ClumpType.Backdrop => "backdrops",
                _ => throw new NotSupportedException($"Unsupported clump type {Type}")
            }, name + FileExtension);
    }

    public class Clump : AResourceManager<ClumpInfo, ClumpBuffers>
    {
        private readonly ITagContainer diContainer;

        public Clump(ITagContainer diContainer) => this.diContainer = diContainer;

        protected override ClumpBuffers Load(ClumpInfo info) => new ClumpBuffers(diContainer, info.Path);

        protected override void OnResourceLoaded(in Entity entity, ClumpInfo info, ClumpBuffers resource)
        {
            if (resource.Skin != null)
                entity.Set(new Skeleton(resource.Skin));
        }
    }
}
