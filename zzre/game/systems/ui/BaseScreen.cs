using System;
using System.Numerics;
using DefaultEcs.System;
using KeyCode = Silk.NET.SDL.KeyCode;

namespace zzre.game.systems.ui;

public abstract partial class BaseScreen<TComponent, TMessage> : AEntitySetSystem<float>
{
    [Flags]
    protected enum BlockFlags
    {
        None = 0,
        All = DisableGameUpdate | LockPlayerControl | NotifyGameScreen,

        DisableGameUpdate = (1 << 0),
        LockPlayerControl = (1 << 1),
        NotifyGameScreen = (1 << 2)
    }

    private readonly BlockFlags blockFlags;
    private readonly IDisposable openSubscription;
    private readonly IDisposable addedSubscription;
    private readonly IDisposable removedSubscription;
    protected readonly IZanzarahContainer zzContainer;
    protected readonly Zanzarah zanzarah;
    protected Inventory inventory => zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
    protected readonly UI ui;
    protected readonly DefaultEcs.World uiWorld;// necessary as Dialog UI is owned by the game and not by the UI.
                                                // Probably worth some cleanup but this would mean merging 
                                                // Game and UI world which I did not decided to be a good idea
    protected readonly UIBuilder preload;
    protected event Action<DefaultEcs.Entity, components.ui.ElementId>? OnElementDown;
    protected event Action<DefaultEcs.Entity, components.ui.ElementId>? OnElementUp;
    protected event Action? OnRightClick;

    protected Vector2 ScreenCenter => ui.LogicalScreen.Center;

    protected BaseScreen(ITagContainer diContainer, BlockFlags blockFlags) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        this.blockFlags = blockFlags;
        zzContainer = diContainer.GetTag<IZanzarahContainer>();
        zanzarah = diContainer.GetTag<Zanzarah>();
        ui = diContainer.GetTag<UI>();
        uiWorld = ui.GetTag<DefaultEcs.World>();
        preload = ui.GetTag<UIBuilder>();
        World.SetMaxCapacity<TComponent>(1);
        openSubscription = World.Subscribe<TMessage>(HandleOpen);
        addedSubscription = World.SubscribeEntityComponentAdded<TComponent>(HandleAdded);
        removedSubscription = World.SubscribeEntityComponentRemoved<TComponent>(HandleRemoved);
    }

    public override void Dispose()
    {
        base.Dispose();
        openSubscription.Dispose();
        addedSubscription.Dispose();
        removedSubscription.Dispose();
        HandleRemoved(default, default!);
    }

    protected virtual void HandleAdded(in DefaultEcs.Entity entity, in TComponent _)
    {
        zzContainer.OnMouseDown += HandleMouseDown;
        zzContainer.OnMouseUp += HandleMouseUp;
        zzContainer.OnKeyDown += HandleKeyDown;

        if (zanzarah.CurrentGame != null && blockFlags.HasFlag(BlockFlags.DisableGameUpdate))
            zanzarah.CurrentGame.IsUpdateEnabled = false;
        if (blockFlags.HasFlag(BlockFlags.LockPlayerControl))
            zanzarah.CurrentGame?.Publish(messages.LockPlayerControl.Forever);
        if (blockFlags.HasFlag(BlockFlags.NotifyGameScreen))
            zanzarah.CurrentGame?.Publish<messages.ui.GameScreenOpened>();
    }

    protected virtual void HandleRemoved(in DefaultEcs.Entity entity, in TComponent _)
    {
        zzContainer.OnMouseDown -= HandleMouseDown;
        zzContainer.OnMouseUp -= HandleMouseUp;
        zzContainer.OnKeyDown -= HandleKeyDown;

        if (zanzarah.CurrentGame != null && blockFlags.HasFlag(BlockFlags.DisableGameUpdate))
            zanzarah.CurrentGame.IsUpdateEnabled = true;
        if (blockFlags.HasFlag(BlockFlags.LockPlayerControl))
            zanzarah.CurrentGame?.Publish(messages.LockPlayerControl.Unlock);
        if (blockFlags.HasFlag(BlockFlags.NotifyGameScreen))
            zanzarah.CurrentGame?.Publish<messages.ui.GameScreenClosed>();
    }

    private void HandleMouseDown(MouseButton button, Vector2 pos) => HandleMouse(button, pos, isDown: true);
    private void HandleMouseUp(MouseButton button, Vector2 pos) => HandleMouse(button, pos, isDown: false);
    private void HandleMouse(MouseButton button, Vector2 pos, bool isDown)
    {
        if (button == MouseButton.Right)
        {
            if (isDown)
                OnRightClick?.Invoke();
            return;
        }
        if (button != MouseButton.Left)
            return;

        if (!uiWorld.Has<components.ui.HoveredElement>())
            return;
        // TODO: Check whether multiple screens could respond to the same element click
        var hovered = uiWorld.Get<components.ui.HoveredElement>();
        if (isDown)
            OnElementDown?.Invoke(hovered.Entity, hovered.Id);
        else
            OnElementUp?.Invoke(hovered.Entity, hovered.Id);
    }

    protected virtual void HandleKeyDown(KeyCode key)
    {
    }

    protected abstract void HandleOpen(in TMessage message);

    [Update]
    protected virtual void Update(float timeElapsed, in DefaultEcs.Entity entity, ref TComponent component) { }

}
