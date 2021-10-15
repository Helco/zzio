using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems
{
    public class Animal : BaseDisposable, ISystem<float>
    {
        private readonly Scene scene;
        private readonly DefaultEcs.World ecsWorld;
        private readonly IDisposable sceneLoadSubscription;
        private readonly SyncedLocationSystem syncedLocationSystem;

        public Animal(ITagContainer diContainer)
        {
            scene = diContainer.GetTag<Scene>();
            ecsWorld = diContainer.GetTag<DefaultEcs.World>();
            sceneLoadSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
            syncedLocationSystem = diContainer.GetTag<SyncedLocationSystem>();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            sceneLoadSubscription.Dispose();
        }

        public bool IsEnabled
        {
            get => true;
            set => throw new InvalidOperationException();
        }

        public void Update(float state)
        {
        }

        private void HandleSceneLoaded(in messages.SceneLoaded message)
        {
            foreach (var trigger in scene.triggers.Where(t => t.type == TriggerType.Animal))
            {
                var entity = ecsWorld.CreateEntity();
                var location = syncedLocationSystem.AddTo(entity);
                location.LocalPosition = trigger.pos.ToNumerics();
                location.LocalRotation = trigger.dir.ToNumericsRotation();
            }
        }
    }
}
