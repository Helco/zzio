namespace zzre.game.systems;
using System;
using System.Linq;
using DefaultEcs.System;
using zzio.scn;

[PauseDuring(PauseTrigger.UIScreen)]
public partial class Doorway : AEntitySetSystem<float>
{
    private readonly Game game;
    private readonly IDisposable doorwayTriggerDisposable;

    private string targetScene = "";
    private int targetEntry;
    private float fadeOffTime; // just a placeholder, will be removed after actual fade off exists

    public Doorway(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        game = diContainer.GetTag<Game>();
        doorwayTriggerDisposable = World.SubscribeComponentAdded<components.ActiveTrigger>(HandleActiveTrigger);
    }

    public override void Dispose()
    {
        base.Dispose();
        doorwayTriggerDisposable?.Dispose();
    }

    private void HandleActiveTrigger(in DefaultEcs.Entity entity, in components.ActiveTrigger value)
    {
        if (!IsEnabled)
            return;

        var trigger = entity.Get<Trigger>();
        if (trigger.type != TriggerType.Doorway)
            return;

        World.Get<components.PlayerEntity>().Entity.Set(components.GameFlow.Doorway);
        targetScene = $"sc_{trigger.ii3}";
        targetEntry = (int)trigger.ii2;
        fadeOffTime = 0.6f;
        World.Publish(messages.LockPlayerControl.Forever);
        // TODO: Add fade off during scene changes
        // TODO: Fade off music and ambient sounds during scene changes
    }

    [WithPredicate]
    private static bool IsInGameFlow(in components.GameFlow flow) => flow == components.GameFlow.Doorway;

    [Update]
    private new void Update(float elapsedTime, in DefaultEcs.Entity entity)
    {
        if (fadeOffTime <= 0f)
            return;
        fadeOffTime -= elapsedTime;
        if (fadeOffTime > 0f)
            return;

        var prevCount = World.Count();
        World.Publish(new messages.SceneChanging());
        Console.WriteLine($"Entity count before: {prevCount} after {World.Count()}");
        game.LoadScene(targetScene);
        World.Publish(new messages.PlayerEntered(FindEntryTrigger()));
        entity.Set(components.GameFlow.Normal);
    }

    private Trigger? TryFindTrigger(TriggerType type, int ii1 = -1)
    {
        var triggerEntity = World
            .GetEntities()
            .With((in Trigger t) => t.type == type && (ii1 < 0 || t.ii1 == ii1))
            .AsEnumerable()
            .FirstOrDefault();
        return triggerEntity == default
            ? null
            : triggerEntity.Get<Trigger>();
    }

    private Trigger FindEntryTrigger() => (targetEntry < 0
        ? (TryFindTrigger(TriggerType.SingleplayerStartpoint)
        ?? TryFindTrigger(TriggerType.SavePoint)
        ?? TryFindTrigger(TriggerType.MultiplayerStartpoint))

        : TryFindTrigger(TriggerType.Doorway, targetEntry)
        ?? TryFindTrigger(TriggerType.Elevator, targetEntry)
        ?? TryFindTrigger(TriggerType.RuneTarget, targetEntry))

        ?? throw new System.IO.InvalidDataException($"Scene {targetScene} does not have suitable entry trigger for {targetEntry}");
}
