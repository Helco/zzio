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
    private readonly IDisposable generateMessageSubscription;
    public bool IsEnabled { get; set; }

    public AIPath(ITagContainer diContainer)
    {
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        componentRemovedSubscription = ecsWorld.SubscribeEntityComponentRemoved<components.AIPath>(HandleComponentRemoved);
        generateMessageSubscription = ecsWorld.Subscribe<messages.GenerateAIPath>(HandleGenerateAIPath);
    }

    public void Dispose()
    {
        componentRemovedSubscription.Dispose();
        generateMessageSubscription.Dispose();
    }

    private void HandleComponentRemoved(in DefaultEcs.Entity entity, in components.AIPath value)
    {
        value.WaypointIndices.Dispose();
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


    }

    public void Update(float _)
    {
    }
}
