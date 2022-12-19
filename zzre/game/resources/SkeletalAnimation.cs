using DefaultEcs.Resource;
using zzio;
using zzio.vfs;

namespace zzre.game.resources
{
    public class SkeletalAnimation : AResourceManager<(AnimationType type, string fileName), zzio.SkeletalAnimation>
    {
        private static readonly FilePath BasePath = new("resources/models/actorsex");

        private readonly IResourcePool resourcePool;

        public SkeletalAnimation(ITagContainer diContainer)
        {
            resourcePool = diContainer.GetTag<IResourcePool>();
            Manage(diContainer.GetTag<DefaultEcs.World>());
        }

        protected override zzio.SkeletalAnimation Load((AnimationType type, string fileName) info)
        {
            var path = BasePath.Combine(info.fileName);
            using var stream = resourcePool.FindAndOpen(path)
                ?? throw new System.IO.FileNotFoundException($"Could not open animation: {path}");
            return zzio.SkeletalAnimation.ReadNew(stream);
        }

        protected override void OnResourceLoaded(in DefaultEcs.Entity entity, (AnimationType type, string fileName) info, zzio.SkeletalAnimation resource)
        {
            if (entity.Has<components.AnimationPool>())
                entity.Get<components.AnimationPool>().Add(info.type, resource);
            else
                entity.Set(components.AnimationPool.CreateWith(info.type, resource));
        }
    }
}
