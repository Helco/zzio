using System;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems.ui
{
    public abstract partial class BaseScreen<TComponent, TMessage> : AEntitySetSystem<float>
    {
        private readonly IDisposable openSubscription;
        private readonly IDisposable addedSubscription;
        private readonly IDisposable removedSubscription;
        protected readonly IZanzarahContainer zzContainer;
        protected readonly Zanzarah zanzarah;
        protected readonly UI ui;
        protected readonly UIPreloader preload;
        protected event Action<DefaultEcs.Entity, components.ui.ElementId>? OnElementDown;
        protected event Action<DefaultEcs.Entity, components.ui.ElementId>? OnElementUp;

        protected Vector2 ScreenCenter => ui.LogicalScreen.Center;

        protected BaseScreen(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
            zzContainer = diContainer.GetTag<IZanzarahContainer>();
            zanzarah = diContainer.GetTag<Zanzarah>();
            ui = diContainer.GetTag<UI>();
            preload = diContainer.GetTag<UIPreloader>();
            World.SetMaxCapacity<TComponent>(1);
            openSubscription = World.Subscribe<TMessage>(HandleOpen);
            addedSubscription = World.SubscribeComponentAdded<TComponent>(HandleAdded);
            removedSubscription = World.SubscribeComponentRemoved<TComponent>(HandleRemoved);
        }

        public override void Dispose()
        {
            base.Dispose();
            openSubscription.Dispose();
            addedSubscription.Dispose();
            removedSubscription.Dispose();
            HandleRemoved(default, default!);
            RemoveContainerEvents();
        }

        private void HandleAdded(in DefaultEcs.Entity entity, in TComponent _)
        {
            zzContainer.OnMouseDown += HandleMouseDown;
            zzContainer.OnMouseUp += HandleMouseUp;
            zzContainer.OnKeyDown += HandleKeyDown;
            if (zanzarah.CurrentGame != null)
                zanzarah.CurrentGame.IsUpdateEnabled = false;
            zanzarah.CurrentGame?.Publish(messages.LockPlayerControl.Forever);
        }

        private void RemoveContainerEvents()
        {
            zzContainer.OnMouseDown -= HandleMouseDown;
            zzContainer.OnMouseUp -= HandleMouseUp;
            zzContainer.OnKeyDown -= HandleKeyDown;
        }

        private void HandleRemoved(in DefaultEcs.Entity entity, in TComponent _)
        {
            RemoveContainerEvents();
            if (zanzarah.CurrentGame != null)
                zanzarah.CurrentGame.IsUpdateEnabled = true;
            zanzarah.CurrentGame?.Publish(messages.LockPlayerControl.Unlock);
        }

        private void HandleMouseDown(Veldrid.MouseButton button, Vector2 pos) => HandleMouse(button, pos, isDown: true);
        private void HandleMouseUp(Veldrid.MouseButton button, Vector2 pos) => HandleMouse(button, pos, isDown: false);
        private void HandleMouse(Veldrid.MouseButton button, Vector2 pos, bool isDown)
        {
            if (button != Veldrid.MouseButton.Left)
                return;

            if (!World.Has<components.ui.HoveredElement>())
                return;
            var hovered = World.Get<components.ui.HoveredElement>();
            if (isDown)
                OnElementDown?.Invoke(hovered.Entity, hovered.Id);
            else
                OnElementUp?.Invoke(hovered.Entity, hovered.Id);
        }

        protected virtual void HandleKeyDown(Veldrid.Key key)
        {
        }

        protected abstract void HandleOpen(in TMessage message);

        [Update]
        protected abstract void Update(float timeElapsed, in DefaultEcs.Entity entity, ref TComponent component);

    }
}