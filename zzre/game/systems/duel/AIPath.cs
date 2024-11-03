using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DefaultEcs.System;

namespace zzre.game.systems;

public sealed partial class AIPath : ISystem<float>
{
    private const int DefaultPathLength = 128;

    private readonly DefaultEcs.World ecsWorld;
    private readonly IDisposable componentRemovedSubscription;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly IDisposable generateMessageSubscription;
    private PathFinder pathFinder = null!;
    public bool IsEnabled { get; set; }

    public AIPath(ITagContainer diContainer)
    {
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        ecsWorld.SetMaxCapacity<PathFinder>(1);
        componentRemovedSubscription = ecsWorld.SubscribeEntityComponentRemoved<components.AIPath>(HandleComponentRemoved);
        sceneLoadedSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        generateMessageSubscription = ecsWorld.Subscribe<messages.GenerateAIPath>(HandleGenerateAIPath);
    }

    public void Dispose()
    {
        componentRemovedSubscription.Dispose();
        sceneLoadedSubscription.Dispose();
        generateMessageSubscription.Dispose();
    }

    private void HandleComponentRemoved(in DefaultEcs.Entity entity, in components.AIPath value)
    {
        value.WaypointIndices.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded msg)
    {
        pathFinder = new PathFinder(msg.Scene.waypointSystem, ecsWorld.Get<WorldCollider>());
        ecsWorld.Set(pathFinder);
    }

    private void HandleGenerateAIPath(in messages.GenerateAIPath message)
    {
        var optPath = message.ForEntity.TryGet<components.AIPath>();
        if (!optPath.HasValue)
        {
            message.ForEntity.Set<components.AIPath>(new()
            {
                WaypointIndices = new(DefaultPathLength)
            });
            optPath = new(ref message.ForEntity.Get<components.AIPath>());
        }
        ref var path = ref optPath.Value;
        path.WaypointIndices.Clear();

        uint nearestId = message.CurrentWaypointId;
        if (message.CurrentWaypointId == PathFinder.InvalidId)
        {
            var currentPosition = message.CurrentPosition ?? message.ForEntity.Get<Location>().GlobalPosition;
            nearestId = pathFinder.NearestTraversableId(currentPosition);
        }
        if (nearestId == PathFinder.InvalidId)
            return;

        path.WaypointIndices.Add(nearestId);
        var currentId = nearestId;
        while (path.WaypointIndices.Count < 4)
        {
            currentId = pathFinder.TryRandomNextTraversable(currentId, path.WaypointIndices, out _);
            if (currentId == PathFinder.InvalidId)
                return;
            path.WaypointIndices.Add(currentId);
        }
    }

    public void Update(float _)
    {
    }
}
