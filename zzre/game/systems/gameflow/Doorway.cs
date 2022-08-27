namespace zzre.game.systems;
using System;
using System.Linq;
using DefaultEcs.System;
using zzio.scn;

[PauseDuring(PauseTrigger.UIScreen)]
public partial class Doorway : ISystem<float>
{
    private readonly DefaultEcs.World ecsWorld;
    private readonly Game game;
    private readonly IDisposable doorwayTriggerDisposable;

    private string targetScene = "";
    private int targetEntry;
    private float fadeOffTime; // just a placeholder, will be removed after actual fade off exists

    public bool IsEnabled { get; set; } = true;

    public Doorway(ITagContainer diContainer)
    {
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        game = diContainer.GetTag<Game>();
        doorwayTriggerDisposable = ecsWorld.SubscribeComponentAdded<components.ActiveTrigger>(HandleActiveTrigger);
    }

    public void Dispose()
    {
        doorwayTriggerDisposable?.Dispose();
    }

    private void HandleActiveTrigger(in DefaultEcs.Entity entity, in components.ActiveTrigger value)
    {
        if (!IsEnabled)
            return;

        var trigger = entity.Get<Trigger>();
        if (trigger.type != TriggerType.Doorway)
            return;

        targetScene = $"sc_{trigger.ii3}";
        targetEntry = (int)trigger.ii2;
        fadeOffTime = 0.6f;
        ecsWorld.Publish(messages.LockPlayerControl.Forever);
        // TODO: Add fade off during scene changes
        // TODO: Fade off music and ambient sounds during scene changes
    }

    public void Update(float elapsedTime)
    {
        if (fadeOffTime <= 0f)
            return;
        fadeOffTime -= elapsedTime;
        if (fadeOffTime > 0f)
            return;

        ecsWorld.Publish(new messages.SceneChanging());
        game.LoadScene(targetScene);
        ecsWorld.Publish(new messages.PlayerEntered(FindEntryTrigger()));
    }

    private Trigger? TryFindTrigger(TriggerType type, int ii1 = -1)
    {
        var triggerEntity = ecsWorld
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
