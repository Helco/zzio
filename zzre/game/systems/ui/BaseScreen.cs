using System;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems.ui
{
    public abstract partial class BaseScreen<TComponent, TMessage> : AEntitySetSystem<float>
    {
        private readonly IDisposable openSubscription;
        protected readonly UI ui;
        protected readonly UIPreloader preload;

        protected Vector2 ScreenCenter => ui.LogicalScreen.Center;

        protected BaseScreen(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
            ui = diContainer.GetTag<UI>();
            preload = diContainer.GetTag<UIPreloader>();
            World.SetMaxCapacity<TComponent>(1);
            openSubscription = World.Subscribe<TMessage>(HandleOpen);
        }

        public override void Dispose()
        {
            base.Dispose();
            openSubscription.Dispose();
        }

        protected abstract void HandleOpen(in TMessage message);

        [Update]
        protected abstract void Update(float timeElapsed, ref TComponent component);

    }
}