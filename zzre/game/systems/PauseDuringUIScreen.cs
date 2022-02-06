using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;

namespace zzre.game.systems
{
    public class PauseDuringUIScreen : ISystem<float>
    {
        private readonly ISystem<float>[] systems;
        private readonly IDisposable openSubscription;
        private readonly IDisposable closeSubscription;

        public bool IsEnabled { get; set; }

        public PauseDuringUIScreen(ITagContainer diContainer, IReadOnlyList<ISystem<float>> allSystems)
        {
            systems = allSystems
                .Where(s => s.GetType().CustomAttributes.Any(d => d.AttributeType == typeof(PauseDuringUIScreenAttribute)))
                .ToArray();
            var ecsWorld = diContainer.GetTag<DefaultEcs.World>();
            openSubscription = ecsWorld.Subscribe<messages.ui.GameScreenOpened>(HandleOpened);
            closeSubscription = ecsWorld.Subscribe<messages.ui.GameScreenClosed>(HandleClosed);
        }

        private void HandleOpened(in messages.ui.GameScreenOpened message)
        {
            foreach (var system in systems)
                system.IsEnabled = false;
        }

        private void HandleClosed(in messages.ui.GameScreenClosed message)
        {
            foreach (var system in systems)
                system.IsEnabled = true;
        }

        public void Dispose()
        {
        }

        public void Update(float state)
        {
        }
    }
}
