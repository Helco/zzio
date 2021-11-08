using System;
using System.Linq;
using DefaultEcs.Resource;
using DefaultEcs.System;
using zzio;
using zzio.scn;

using AnimalWaypointAIConfig = zzre.game.components.AnimalWaypointAI.Configuration;

namespace zzre.game.systems
{
    public class Animal : BaseDisposable, ISystem<float>
    {
        private readonly Scene scene;
        private readonly DefaultEcs.World ecsWorld;
        private readonly IDisposable sceneLoadSubscription;

        public Animal(ITagContainer diContainer)
        {
            scene = diContainer.GetTag<Scene>();
            ecsWorld = diContainer.GetTag<DefaultEcs.World>();
            sceneLoadSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            sceneLoadSubscription.Dispose();
        }

        public bool IsEnabled { get; set; } = true;

        public void Update(float state)
        {
        }

        private void HandleSceneLoaded(in messages.SceneLoaded message)
        {
            if (!IsEnabled)
                return;

            foreach (var trigger in scene.triggers
                .Where(t => t.type == TriggerType.Animal)
                .Where(t =>
                    t.ii1 != (uint)AnimalType.PooledBird &&
                    t.ii1 != (uint)AnimalType.CollectionFairy &&
                    t.ii1 != (uint)AnimalType.Unused))
            {
                var entity = ecsWorld.CreateEntity();
                entity.Set(trigger);

                var location = new Location();
                location.Parent = ecsWorld.Get<Location>();
                location.LocalPosition = trigger.pos.ToNumerics();
                location.LocalRotation = trigger.dir.ToNumericsRotation();
                entity.Set(location);

                var type = (AnimalType)trigger.ii1;
                var actorFile = ChooseActorFile(type);
                if (actorFile != null)
                {
                    entity.Set(ManagedResource<ActorExDescription>.Create(actorFile));
                    var body = entity.Get<components.ActorParts>().Body;
                    body.Get<Skeleton>().JumpToAnimation(
                        body.Get<components.AnimationPool>()[AnimationType.Idle0]);
                }

                switch(type)
                {
                    case AnimalType.Butterfly:
                        entity.Set(new components.Butterfly(trigger.ii2, GlobalRandom.Get));
                        break;
                    case AnimalType.Dragonfly:
                        entity.Set(new components.AnimalWaypointAI(AnimalWaypointAIConfig.Dragonfly));
                        break;
                    case AnimalType.Frog:
                        entity.Set(new components.AnimalWaypointAI(AnimalWaypointAIConfig.Frog));
                        break;
                    case AnimalType.CirclingBird: entity.Set<components.CirclingBird>();
                        break;
                    case AnimalType.Firefly:
                        entity.Set(new components.AnimalWaypointAI(AnimalWaypointAIConfig.Firefly));
                        break;
                    case AnimalType.Bug:
                        entity.Set(new components.AnimalWaypointAI(AnimalWaypointAIConfig.Bug));
                        break;
                    case AnimalType.Rabbit:
                        entity.Set(new components.AnimalWaypointAI(AnimalWaypointAIConfig.Rabbit));
                        break;
                    case AnimalType.Chicken:
                        entity.Set(new components.AnimalWaypointAI(AnimalWaypointAIConfig.Chicken));
                        break;
                    case AnimalType.BlackPixie:
                        entity.Set(new components.AnimalWaypointAI(AnimalWaypointAIConfig.BlackPixie));
                        break;
                }
            }
        }

        private static string ChooseBetween(string a1, string a2) => GlobalRandom.Get.Next(2) > 0 ? a1 : a2;
        private static string? ChooseActorFile(AnimalType type) => type switch
        {
            AnimalType.Butterfly => ChooseBetween("a000sa00", "a001sa00"),
            AnimalType.Dragonfly => "a002sa01",
            AnimalType.PooledBird => ChooseBetween("a003sa02", "a005sa04"),
            AnimalType.Frog => "a004sa03",
            AnimalType.CirclingBird => "a005sa04",
            AnimalType.Bug => ChooseBetween("a006sa05", "a007sa06"),
            AnimalType.Rabbit => "a008sa07",
            AnimalType.Chicken => ChooseBetween("a020sa20", "a021sa20"),
            AnimalType.BlackPixie => "u010s10m",

            _ => null
        };
    }
}
