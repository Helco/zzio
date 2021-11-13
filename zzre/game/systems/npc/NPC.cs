using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems
{
    public partial class NPC : AEntitySetSystem<float>
    {
        private const string DefaultActor = "p000s00m"; // pixie...
        private const float GroundFromOffset = 1f;
        private const float GroundToOffset = -7f;

        private readonly IDisposable sceneLoadSubscription;
        private readonly WorldCollider worldCollider;
        private readonly Scene scene;
        private readonly zzio.db.MappedDB mappedDB;

        public NPC(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, null, 0)
        {
            worldCollider = diContainer.GetTag<WorldCollider>();
            scene = diContainer.GetTag<Scene>();
            mappedDB = diContainer.GetTag<zzio.db.MappedDB>();
            sceneLoadSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        }

        private void HandleSceneLoaded(in messages.SceneLoaded message)
        {
            if (!IsEnabled)
                return;

            foreach (var trigger in scene.triggers.Where(t => t.type == TriggerType.NpcStartpoint))
            {
                var entity = World.CreateEntity();
                var location = new Location();
                location.Parent = World.Get<Location>();
                location.LocalPosition = trigger.pos.ToNumerics();
                location.LocalRotation = trigger.dir.ToNumericsRotation();
                location.LookIn(location.InnerForward with { Y = 0f });
                entity.Set(location);

                var dbUID = new zzio.primitives.UID(trigger.ii1);
                if (!mappedDB.TryGetNpc(dbUID, out var dbRow))
                    throw new System.IO.InvalidDataException($"Invalid NPC database UID: {dbUID}");
                entity.Set(dbRow);

                if (dbRow.InitScript.Length > 0)
                    entity.Set(new components.ScriptExecution(dbRow.InitScript));                
                entity.Set<components.NPC>();
                entity.Set<components.NPCModifier>();
            }

            World.Publish(default(messages.ExecuteNPCScript));

            var npcScripts = World
                .GetEntities()
                .With<components.ScriptExecution>()
                .With<zzio.db.NpcRow>()
                .AsEnumerable()
                .ToArray();
            foreach (var entity in npcScripts)
            {
                var dbRow = entity.Get<zzio.db.NpcRow>();
                if (dbRow.UpdateScript.Length > 0 && false)
                    entity.Set(new components.ScriptExecution(dbRow.UpdateScript));
                else
                    entity.Remove<components.ScriptExecution>();
            }

            var npcs = World
                .GetEntities()
                .With<zzio.db.NpcRow>()
                .AsEnumerable();
            foreach (var entity in npcs)
            {
                if (entity.Has<components.ActorParts>())
                {
                    entity.Set(new components.NonFairyAnimation(GlobalRandom.Get));
                    var body = entity.Get<components.ActorParts>().Body;
                    body.Set(new components.PuppetActorMovement()
                    {
                        TargetDirection = entity.Get<Location>().InnerForward
                    });
                }

                if (!entity.Has<components.NPCType>())
                    entity.Set(components.NPCType.Biped);
                var npcType = entity.Get<components.NPCType>();
                if (npcType != components.NPCType.Flying && entity.Has<Sphere>())
                {
                    PutOnGround(entity);
                }
            }
        }

        [Update]
        private void Update(float elapsedTime, ref components.NPC npc)
        {
        }

        private void PutOnGround(DefaultEcs.Entity entity)
        {
            var collider = entity.Get<Sphere>();
            var location = entity.Get<Location>();
            var cast = worldCollider.Cast(new Line(
                location.LocalPosition + Vector3.UnitY * GroundFromOffset,
                location.LocalPosition + Vector3.UnitY * GroundToOffset));
            if (cast != null)
                location.LocalPosition = cast.Value.Point + Vector3.UnitY * collider.Radius / 2f;
        }
    }
}
