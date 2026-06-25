using System;
using System.Diagnostics;
using DefaultEcs.System;
using Serilog;

namespace zzre.game.systems;

public partial class DebugProtection : ISystem<float>
{
    private readonly IDisposable entityDisposedSubscription;
    private readonly ILogger logger;

    public DebugProtection(ITagContainer diContainer)
    {
        logger = diContainer.GetLoggerFor<DebugProtection>();
        entityDisposedSubscription = diContainer.GetTag<DefaultEcs.World>().SubscribeEntityDisposed(HandleEntityDisposed);
    }

    public bool IsEnabled
    {
        get => true;
        set { }
    }

    public void Dispose()
    {
        entityDisposedSubscription?.Dispose();
    }

    public void Update(float state) { }

    private void HandleEntityDisposed(in DefaultEcs.Entity entity)
    {
        if (!entity.Has<components.DebugProtected>())
            return;
        logger.Error("Protected entity {Entity} was disposed", entity);
        if (Debugger.IsAttached)
            Debugger.Break();
    }
}
