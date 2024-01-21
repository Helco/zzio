using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems;

public partial class NPC : AEntitySetSystem<float>
{
    private const uint MaxEnabledII2 = 1000;

    private readonly IDisposable sceneChangingSubscription;
    private readonly IDisposable sceneLoadSubscription;
    private readonly IDisposable setNpcModifierSubscription;
    private readonly zzio.db.MappedDB mappedDB;

    public NPC(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        mappedDB = diContainer.GetTag<zzio.db.MappedDB>();
        sceneChangingSubscription = World.Subscribe<messages.SceneChanging>(HandleSceneChanging);
        sceneLoadSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        setNpcModifierSubscription = World.Subscribe<zzio.GSModSetNPCModifier>(HandleSetNpcModifier);
    }

    public override void Dispose()
    {
        base.Dispose();
        sceneChangingSubscription.Dispose();
        sceneLoadSubscription.Dispose();
        setNpcModifierSubscription.Dispose();
    }

    private void HandleSceneChanging(in messages.SceneChanging _) => Set.DisposeAll();

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        if (!IsEnabled)
            return;

        var triggers = World
            .GetEntities()
            .With((in Trigger t) => t.type == TriggerType.NpcStartpoint)
            .AsEnumerable()
            .ToArray();
        foreach (var trigger in triggers.Where(t => t.Get<Trigger>().ii2 >= MaxEnabledII2))
            trigger.Disable();

        var scene = message.Scene;
        foreach (var trigger in scene.triggers.Where(t => t.type == TriggerType.NpcStartpoint && t.ii2 < MaxEnabledII2))
        {
            var entity = World.CreateEntity();
            entity.Set(trigger);

            var location = new Location()
            {
                Parent = World.Get<Location>(),
                LocalPosition = trigger.pos,
                LocalRotation = trigger.dir.ToZZRotation()
            };
            location.LookIn(location.InnerForward with { Y = 0f });
            entity.Set(location);

            var dbUID = new zzio.UID(trigger.ii1);
            if (!mappedDB.TryGetNpc(dbUID, out var dbRow))
            {
                // another crazy special case fallback... this would happen for every non-existant UID
                if (dbUID != new zzio.UID(0x97182704) || !mappedDB.TryGetNpc(new zzio.UID(0x8219b694), out dbRow))
                    throw new System.IO.InvalidDataException($"Invalid NPC database UID: {dbUID}");
            }
            entity.Set(dbRow);

            if (dbRow.InitScript.Length > 0)
                entity.Set(new components.ScriptExecution(dbRow.InitScript));
            entity.Set<components.NPCType>(); // default is Biped
            entity.Set(components.NPCState.Script);
            entity.Set(components.NPCMovement.Default);
            entity.Set<components.NPCIdle>();
            entity.Set<components.NPCModifier>();
            entity.Set<components.NPCLookAtPlayer>();
            entity.Set<components.NPCLookAtTrigger>();
            entity.Set<components.Collidable>();
            entity.Set(new Inventory(mappedDB));
        }

        World.Publish(default(messages.ExecuteNPCScript));

        var npcs = World
            .GetEntities()
            .With<zzio.db.NpcRow>()
            .AsEnumerable();
        foreach (var entity in npcs)
        {
            var dbRow = entity.Get<zzio.db.NpcRow>();
            if (dbRow.UpdateScript.Length > 0)
                entity.Set(new components.ScriptExecution(dbRow.UpdateScript));
            else
                entity.Remove<components.ScriptExecution>();

            if (!entity.Has<components.NPCType>())
                entity.Set(components.NPCType.Biped);
            var npcType = entity.Get<components.NPCType>();
            if (npcType != components.NPCType.Flying && entity.Has<Sphere>())
                World.Publish(new messages.CreaturePlaceToGround(entity));

            // seems to only happen in sc_1050, this is a NPC hidden under the map
            // it has Biped type but no model is set, so also no PuppetActorMovement 
            // but it does have a Fairy which spawns and tries to align itself
            if (entity.Get<components.NPCType>() == components.NPCType.Biped &&
                !entity.Has<components.PuppetActorMovement>())
                entity.Set<components.PuppetActorMovement>();

            // TODO: Add Pixie behaviour
            // TODO: Add PlantBlocker behaviour
            // TODO: Add Biped behaviour (HeadIK, open doors)
        }
    }

    [Update]
    private void Update(float elapsedTime, ref components.NPCState npc)
    {
    }

    private void HandleSetNpcModifier(in zzio.GSModSetNPCModifier gsmod)
    {
        foreach (var entity in Set.GetEntities())
        {
            if (entity.Get<Trigger>().idx == gsmod.TriggerId)
            {
                entity.Set(new components.NPCModifier(gsmod.Value));
                return;
            }
        }
    }
}
