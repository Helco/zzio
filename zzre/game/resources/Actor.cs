using DefaultEcs.Resource;
using zzio;
using zzio.vfs;

namespace zzre.game.resources;

public class Actor : AResourceManager<string, ActorExDescription>
{
    private static readonly FilePath BasePath = new("resources/models/actorsex/");
    private const string FileExtension = ".aed";
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
        actorParts.Body.Get<Skeleton>().Location.Parent = actorParts.Body.Get<Location>();

        if (actorParts.Wings.HasValue)
        {
            var skeleton = actorParts.Body.Get<Skeleton>();
            var wingsParentBone = skeleton.Bones[resource.attachWingsToBone];
            actorParts.Wings.Value.Get<Location>().Parent = wingsParentBone;
        }

        entity.Set(actorParts);
    }

    private static DefaultEcs.Entity CreateActorPart(DefaultEcs.Entity parent, ActorPartDescription partDescr)
    {
        var part = parent.World.CreateEntity();
        part.Set<components.SyncedLocation>();
        part.Set(ManagedResource<ClumpBuffers>.Create(ClumpInfoLEGACY.Actor(partDescr.model)));
        part.Set(ManagedResource<zzio.SkeletalAnimation>.Create(partDescr.animations));
        part.Set<components.Visibility>();
        part.Set<components.ActorPart>(); // add *after* resources have been loaded
        part.Set(new components.Parent(parent));
        return part;
    }
}
