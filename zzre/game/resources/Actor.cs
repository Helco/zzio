using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.Resource;
using zzio;
using zzio.utils;
using zzio.vfs;

namespace zzre.game.resources
{
    public class Actor : AResourceManager<string, ActorExDescription>
    {
        private static readonly FilePath BasePath = new FilePath("resources/models/actorsex/");
        private static readonly string FileExtension = ".aed";
        private readonly IResourcePool resourcePool;

        public Actor(ITagContainer diContainer)
        {
            resourcePool = diContainer.GetTag<IResourcePool>();
        }

        protected override ActorExDescription Load(string info)
        {
            var path = BasePath.Combine(info + FileExtension);
            using var stream = resourcePool.FindAndOpen(path) ??
                throw new System.IO.FileNotFoundException($"Could not find actor: {path}");
            return ActorExDescription.ReadNew(stream);
        }

        protected override void OnResourceLoaded(in DefaultEcs.Entity entity, string info, ActorExDescription resource)
        {
            entity.Set(resource);

            var actorParts = new components.ActorParts()
            {
                Body = CreateActorPart(entity, resource.body),
                Wings = resource.HasWings
                    ? CreateActorPart(entity, resource.wings)
                    : null
            };
            actorParts.Body.Set(new Location() { Parent = entity.Get<Location>() });

            if (actorParts.Wings.HasValue)
            {
                var skeleton = actorParts.Body.Get<Skeleton>();
                var wingsParentBone = skeleton.Bones[resource.attachWingsToBone];
                actorParts.Wings.Value.Set(new Location() { Parent = wingsParentBone });
            }
        }

        private static DefaultEcs.Entity CreateActorPart(DefaultEcs.Entity parent, ActorPartDescription partDescr)
        {
            var part = parent.World.CreateEntity();
            part.Set<components.SyncedLocation>();
            part.Set(new components.ActorPart(parent));
            part.Set(ManagedResource<ClumpBuffers>.Create(partDescr.model));
            part.Set(ManagedResource<SkeletalAnimation>.Create(partDescr.animations));
            part.Set<components.Visibility>();
            part.Set(ManagedResource<materials.ModelStandardMaterial>.Create(part
                .Get<ClumpBuffers>()
                .SubMeshes
                .Select(sm => sm.Material)
                .ToArray()));
            return part;
        }
    }
}
