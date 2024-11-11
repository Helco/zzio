using System;
using DefaultEcs.System;

namespace zzre.game.systems;

public sealed partial class AIPath : ISystem<float>
{
    private const int DefaultPathLength = 64;

    private readonly DefaultEcs.World ecsWorld;
    private readonly IDisposable componentRemovedSubscription;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly IDisposable generateMessageSubscription;
    private readonly IDisposable resetMessageSubscription;
    private PathFinder pathFinder = null!;
    public bool IsEnabled { get; set; }

    public AIPath(ITagContainer diContainer)
    {
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        ecsWorld.SetMaxCapacity<PathFinder>(1);
        componentRemovedSubscription = ecsWorld.SubscribeEntityComponentRemoved<components.AIPath>(HandleComponentRemoved);
        sceneLoadedSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        generateMessageSubscription = ecsWorld.Subscribe<messages.GenerateAIPath>(HandleGenerateAIPath);
        resetMessageSubscription = ecsWorld.Subscribe<messages.ResetAIMovement>(HandleResetMovement);
    }

    public void Dispose()
    {
        componentRemovedSubscription.Dispose();
        sceneLoadedSubscription.Dispose();
        generateMessageSubscription.Dispose();
        resetMessageSubscription.Dispose();
    }

    private void HandleComponentRemoved(in DefaultEcs.Entity entity, in components.AIPath value)
    {
        value.WaypointIds.Dispose();
        value.Waypoints.Dispose();
        value.EdgeKinds.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded msg)
    {
        pathFinder = new PathFinder(msg.Scene.waypointSystem, ecsWorld.Get<WorldCollider>());
        ecsWorld.Set(pathFinder);
    }

    private static ref components.AIPath ResetPath(DefaultEcs.Entity entity)
    {
        var optPath = entity.TryGet<components.AIPath>();
        if (!optPath.HasValue)
        {
            entity.Set<components.AIPath>(new()
            {
                WaypointIds = new(DefaultPathLength),
                Waypoints = new(DefaultPathLength),
                EdgeKinds = new(DefaultPathLength),
                CurrentIndex = -1
            });
            optPath = new(ref entity.Get<components.AIPath>());
        }
        ref var path = ref optPath.Value;
        path.WaypointIds.Clear();
        path.CurrentIndex = 0;
        path.LastResult = FindPathResult.NotFound;
        return ref path;
    }

    private void HandleResetMovement(in messages.ResetAIMovement msg)
    {
        ResetPath(msg.ForEntity);
        var nearestId = pathFinder.NearestTraversableId(msg.ForEntity.Get<Location>().GlobalPosition);
        msg.ForEntity.Get<components.AIMovement>().CurrentPos = pathFinder[nearestId];
    }

    private void HandleGenerateAIPath(in messages.GenerateAIPath message)
    {
        ref var path = ref ResetPath(message.ForEntity);

        uint nearestId = message.CurrentWaypointId;
        if (message.CurrentWaypointId == PathFinder.InvalidId)
        {
            var currentPosition = message.CurrentPosition ?? message.ForEntity.Get<Location>().GlobalPosition;
            nearestId = pathFinder.NearestTraversableId(currentPosition);
        }
        if (nearestId == PathFinder.InvalidId)
            return;

        path.WaypointIds.Add(nearestId);
        path.Waypoints.Add(pathFinder[nearestId]);
        path.EdgeKinds.Add(WaypointEdgeKind.None);
        var currentId = nearestId;
        while (path.WaypointIds.Count < 4)
        {
            currentId = pathFinder.TryRandomNextTraversable(currentId, path.WaypointIds, out var edgeKind);
            if (currentId == PathFinder.InvalidId)
                return;
            path.WaypointIds.Add(currentId);
            path.Waypoints.Add(pathFinder[currentId]);
            path.EdgeKinds.Add(edgeKind);
        }
        if (path.WaypointIds.Count > 0)
            path.LastResult = FindPathResult.Success;
    }

    public void Update(float _)
    {
    }
}
