namespace zzre.game.systems;
using System;
using DefaultEcs.System;
using zzio;
using zzio.db;
using zzio.scn;

[PauseDuring(PauseTrigger.UIScreen)]
public partial class Doorway : ISystem<float>
{
    private readonly DefaultEcs.World ecsWorld;
    private readonly UI ui;
    private readonly OverworldGame game;
    private readonly MappedDB db;
    private readonly IDisposable doorwayTriggerDisposable;
    private readonly IDisposable enteredDisposable;

    private UID lastSceneName = UID.Invalid;
    private string targetScene = "";
    private int targetEntry;
    private DefaultEcs.Entity fadeEntity;

    public bool IsEnabled { get; set; } = true;

    public Doorway(ITagContainer diContainer)
    {
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        ui = diContainer.GetTag<UI>();
        game = diContainer.GetTag<OverworldGame>();
        db = diContainer.GetTag<MappedDB>();
        doorwayTriggerDisposable = ecsWorld.SubscribeEntityComponentAdded<components.ActiveTrigger>(HandleActiveTrigger);
        enteredDisposable = ecsWorld.Subscribe<messages.PlayerEntered>(HandlePlayerEntered);
    }

    public void Dispose()
    {
        doorwayTriggerDisposable.Dispose();
        enteredDisposable.Dispose();
    }

    private void HandleActiveTrigger(in DefaultEcs.Entity entity, in components.ActiveTrigger value)
    {
        if (!IsEnabled)
            return;

        var trigger = entity.Get<Trigger>();
        if (trigger.type != TriggerType.Doorway)
            return;

        ecsWorld.Set(components.GameFlow.Doorway);
        targetScene = $"sc_{trigger.ii3:D4}";
        targetEntry = (int)trigger.ii2;
        fadeEntity = ui.Builder.CreateStdFlashFade(parent: default);
        ecsWorld.Publish(messages.LockPlayerControl.Forever);
        ecsWorld.Publish(new messages.PlayerLeaving(targetScene));
        // TODO: Add fade off during scene changes
    }

    public void Update(float elapsedTime)
    {
        if (!IsEnabled || ecsWorld.Get<components.GameFlow>() != components.GameFlow.Doorway)
            return;
        if (fadeEntity == default)
        {
            game.LoadScene(targetScene, () => game.FindEntryTrigger(targetEntry));
            ecsWorld.Set(components.GameFlow.Normal);
        }
        else if (fadeEntity.TryGet<components.ui.Fade>(out var fade) && fade.IsFadedIn)
        {
            // by using `default` as a marker we delay loading the scene by one frame
            // this ensures a black screen during the loading freeze
            fadeEntity = default;
        }
    }

    private void HandlePlayerEntered(in messages.PlayerEntered message)
    {
        var newSceneName = ecsWorld.Get<Scene>().dataset.nameUID;
        if (newSceneName != lastSceneName && newSceneName != UID.Invalid)
        {
            lastSceneName = newSceneName;
            var nameText = db.GetText(newSceneName).Text;
            ui.Publish(new messages.ui.Notification(nameText));
        }
    }
}
