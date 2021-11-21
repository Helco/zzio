using DefaultEcs.Resource;
using zzio;
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
            Manage(diContainer.GetTag<DefaultEcs.World>());
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
            
            // attach to the "grandparent" as only animals are controlled directly by the entity
            actorParts.Body.Get<Location>().Parent = entity.Get<Location>().Parent;

            if (actorParts.Wings.HasValue)
            {
                var skeleton = actorParts.Body.Get<Skeleton>();
                var wingsParentBone = skeleton.Bones[resource.attachWingsToBone];
                actorParts.Wings.Value.Set(new Location() { Parent = wingsParentBone });
            }

            entity.Set(actorParts);
        }

        private static DefaultEcs.Entity CreateActorPart(DefaultEcs.Entity parent, ActorPartDescription partDescr)
        {
            var part = parent.World.CreateEntity();
            part.Set<components.SyncedLocation>();
            part.Set(ManagedResource<ClumpBuffers>.Create(ClumpInfo.Actor(partDescr.model)));
            part.Set(ManagedResource<zzio.SkeletalAnimation>.Create(partDescr.animations));
            part.Set<components.Visibility>();
            part.Set(new components.ActorPart(parent)); // add *after* resources have been loaded
            return part;
        }
    }
}
