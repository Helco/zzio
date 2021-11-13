using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.Resource;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems
{
    public partial class NPC : AEntitySetSystem<float>
    {
        private readonly IDisposable sceneLoadSubscription;
        private readonly Scene scene;
        private readonly zzio.db.MappedDB mappedDB;

        public NPC(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, null, 0)
        {
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
                entity.Set(location);

                var dbUID = new zzio.primitives.UID(trigger.ii1);
                if (!mappedDB.TryGetNpc(dbUID, out var dbRow))
                    throw new System.IO.InvalidDataException($"Invalid NPC database UID: {dbUID}");
                entity.Set(dbRow);

                if (dbRow.InitScript.Length > 0)
                    entity.Set(new components.ScriptExecution(dbRow.InitScript));
                entity.Set<components.NPC>();
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
                if (dbRow.UpdateScript.Length > 0)
                    entity.Set(new components.ScriptExecution(dbRow.UpdateScript));
                else
                    entity.Remove<components.ScriptExecution>();

                if (!entity.Has<components.NPCType>())
                    entity.Set(components.NPCType.Biped);
            }
        }

        [Update]
        private void Update(float elapsedTime, ref components.NPC npc)
        {
        }
    }
}
