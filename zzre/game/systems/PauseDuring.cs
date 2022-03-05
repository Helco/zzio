using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;

namespace zzre.game.systems
{
    public class PauseDuring : ISystem<float>
    {
        private readonly ILookup<PauseTrigger, ISystem<float>> systems;
        private readonly IDisposable openSubscription;
        private readonly IDisposable closeSubscription;

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
        }

        public void Update(float state)
        {
        }
    }
}
