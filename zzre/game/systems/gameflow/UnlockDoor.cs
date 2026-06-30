namespace zzre.game.systems;
using System;
using DefaultEcs.System;
using zzio;
using zzio.db;

public partial class UnlockDoor : ISystem<float>
{
    private enum State
    {
        Initial,
        Camera,
        ShowNotification,
        OpeningDoor,
        Cleanup
    }

    private readonly DefaultEcs.World ecsWorld;
    private readonly UI ui;
    private readonly MappedDB db;
    private readonly IDisposable messageDisposable;

    private State state;
    private float timer;
    private StdItemId keyItem;
    private DefaultEcs.Entity lockEntity, doorEntity;

    private DefaultEcs.Entity PlayerEntity => ecsWorld.Get<components.PlayerEntity>().Entity;

    public bool IsEnabled { get; set; } = true;

    public UnlockDoor(ITagContainer diContainer)
    {
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        ui = diContainer.GetTag<UI>();
        db = diContainer.GetTag<MappedDB>();
        messageDisposable = ecsWorld.Subscribe<messages.UnlockDoor>(HandleMessage);
    }

    public void Dispose()
    {
        messageDisposable.Dispose();
    }

    private void HandleMessage(in messages.UnlockDoor msg)
    {
        (doorEntity, lockEntity, keyItem) = msg;
        state = State.Initial;
        timer = 0f;
        ecsWorld.Set(components.GameFlow.UnlockDoor);
    }

    public void Update(float elapsedTime)
    {
        if (!IsEnabled || ecsWorld.Get<components.GameFlow>() != components.GameFlow.UnlockDoor)
            return;
        switch(state)
        {
            case State.Initial:
                ecsWorld.Publish(messages.LockPlayerControl.Forever);
                ecsWorld.Publish(messages.SetCameraMode.PlayerToBehind(lockEntity));
                PlayerEntity.Set(new components.PuppetActorTarget(lockEntity.Get<Location>()));
                // we could async load s012 here
                state = State.Camera;
                break;
            case State.Camera:
                timer += elapsedTime;
                if (timer > 2.2f)
                {
                    timer = 0f;
                    state = State.ShowNotification;
                    var modelId = lockEntity.Get<components.behaviour.Lock>().ModelId;
                    var location = lockEntity.Get<Location>();
                    ecsWorld.Publish(new GSModRemoveModel(modelId));
                    ecsWorld.Publish(new messages.SpawnEffectCombiner(4002, Position: location.GlobalPosition));
                    ecsWorld.Publish(new messages.SpawnSample("resources/audio/sfx/specials/_s012.wav"));
                }
                break;
            case State.ShowNotification:
                timer += elapsedTime;
                if (timer > 0.5f)
                {
                    timer = 0f;
                    state = State.OpeningDoor;
                    ShowNotification();
                }
                timer += elapsedTime; // original engine had a unintended switch fallthrough
                break;
            case State.OpeningDoor:
                timer += elapsedTime;
                if (timer > 2.5f)
                {
                    state = State.Cleanup;
                    doorEntity.Get<components.behaviour.Door>().IsLocked = false;
                }
                break;
            case State.Cleanup:
            default: // just as an emergency fallback
                state = State.Initial;
                timer = 0f;
                ecsWorld.Publish(messages.LockPlayerControl.Unlock);
                ecsWorld.Publish(messages.SetCameraMode.Overworld);
                ecsWorld.Set(components.GameFlow.Normal);
                PlayerEntity.Remove<components.PuppetActorTarget>();
                break;
        }
    }

    private void ShowNotification()
    {
        var message = db.GetText(new(0xB0895AB1)); // "Use"
        var itemName = db.GetItem((int)keyItem).Name;
        var text = $"{message.Text} {{5*{itemName}}}";
        ui.Publish(new messages.ui.Notification(text, new(CardType.Item, (int)keyItem)));
    }
}
