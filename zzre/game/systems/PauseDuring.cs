using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;

namespace zzre.game.systems;

[Flags]
public enum PauseTrigger
{
    UIScreen = 1 << 0,
    GameFlow = 1 << 1
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class PauseDuringAttribute : Attribute
{
    public PauseTrigger Trigger { get; }

    public PauseDuringAttribute(PauseTrigger trigger) => Trigger = trigger;

    public IEnumerable<PauseTrigger> AllTriggers => Enum
        .GetValues<PauseTrigger>()
        .Where(t => Trigger.HasFlag(t));
}

public class PauseDuring : ISystem<float>
{
    private readonly ILookup<PauseTrigger, ISystem<float>> systems;
    private readonly IDisposable openSubscription;
    private readonly IDisposable closeSubscription;
    private readonly IDisposable gameFlowChangeSubscription;

    public bool IsEnabled { get; set; }

    public PauseDuring(ITagContainer diContainer, IReadOnlyList<ISystem<float>> allSystems)
    {
        systems = allSystems
            .Select(s => (system: s, attribute: Attribute.GetCustomAttribute(s.GetType(), typeof(PauseDuringAttribute)) as PauseDuringAttribute))
            .Where(t => t.attribute != null)
            .SelectMany(t => t.attribute!.AllTriggers.Select(trigger => (system: t.system, trigger)))
            .ToLookup(t => t.trigger, t => t.system);

        var ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        openSubscription = ecsWorld.Subscribe((in messages.ui.GameScreenOpened _) => HandleTrigger(PauseTrigger.UIScreen, false));
        closeSubscription = ecsWorld.Subscribe((in messages.ui.GameScreenClosed _) => HandleTrigger(PauseTrigger.UIScreen, true));
        gameFlowChangeSubscription = ecsWorld.SubscribeEntityComponentChanged<components.GameFlow>(HandleGameFlowChange);
    }

    private void HandleGameFlowChange(in DefaultEcs.Entity _, in components.GameFlow oldValue, in components.GameFlow newValue)
    {
        var isNowNormal = newValue == components.GameFlow.Normal;
        if ((oldValue == components.GameFlow.Normal) != isNowNormal)
            HandleTrigger(PauseTrigger.GameFlow, isNowNormal);
    }

    private void HandleTrigger(PauseTrigger trigger, bool enableSystems)
    {
        if (!systems.Contains(trigger))
            return;
        foreach (var system in systems[trigger])
            system.IsEnabled = enableSystems;
    }

    public void Dispose()
    {
        openSubscription?.Dispose();
        closeSubscription?.Dispose();
        gameFlowChangeSubscription?.Dispose();
    }

    public void Update(float state)
    {
    }
}
