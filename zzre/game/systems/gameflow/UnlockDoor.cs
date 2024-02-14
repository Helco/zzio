namespace zzre.game.systems;
using System;
using DefaultEcs.System;
using zzio;
using zzio.db;

public partial class UnlockDoor : AEntitySetSystem<float>
{
    private enum State
    {
        Initial,
        Camera,
        ShowNotification,
        OpeningDoor,
        Cleanup
    }

    private readonly Game game;
    private readonly UI ui;
    private readonly MappedDB db;
    private readonly IDisposable messageDisposable;

    private State state;
    private float timer;
    private StdItemId keyItem;
    private DefaultEcs.Entity lockEntity, doorEntity;

    public UnlockDoor(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        game = diContainer.GetTag<Game>();
        ui = diContainer.GetTag<UI>();
        db = diContainer.GetTag<MappedDB>();
        messageDisposable = World.Subscribe<messages.UnlockDoor>(HandleMessage);
    }

    public override void Dispose()
    {
        base.Dispose();
        messageDisposable.Dispose();
    }

    private void HandleMessage(in messages.UnlockDoor msg)
    {
        (doorEntity, lockEntity, keyItem) = msg;
        state = State.Initial;
        timer = 0f;
        game.PlayerEntity.Set(components.GameFlow.UnlockDoor);
    }

    [WithPredicate]
    private static bool IsInGameFlow(in components.GameFlow flow) => flow == components.GameFlow.UnlockDoor;

    [Update]
    private new void Update(float elapsedTime, in DefaultEcs.Entity entity)
    {
        switch(state)
        {
            case State.Initial:
                World.Publish(messages.LockPlayerControl.Forever);
                World.Publish(messages.SetCameraMode.PlayerToBehind(lockEntity));
                game.PlayerEntity.Set(new components.PuppetActorTarget(lockEntity.Get<Location>()));
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
                    World.Publish(new GSModRemoveModel(modelId));
                    World.Publish(new messages.SpawnEffectCombiner(4002, Position: location.GlobalPosition));
                    // TODO: Play soundeffect on opening lock
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
                World.Publish(messages.LockPlayerControl.Unlock);
                World.Publish(messages.SetCameraMode.Overworld);
                game.PlayerEntity.Set(components.GameFlow.Normal);
                game.PlayerEntity.Remove<components.PuppetActorTarget>();
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
