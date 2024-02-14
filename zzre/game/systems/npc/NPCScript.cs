namespace zzre.game.systems;
using System;
using System.Numerics;
using DefaultEcs.System;
using DefaultEcs.Resource;
using DefaultEcs.Command;
using System.Linq;
using zzio.scn;
using zzio.db;
using zzio;
using zzre.rendering;

[PauseDuring(PauseTrigger.UIScreen)]
public partial class NPCScript : BaseScript<NPCScript>
{
    private const float FlyingColliderSize = 0.2f;
    private const float ItemColliderSizeOffset = -0.05f;

    private readonly IDisposable executeScriptSubscription;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly Game game;
    private readonly MappedDB db;
    private readonly EntityCommandRecorder recorder;
    private Location playerLocation => playerLocationLazy.Value;
    private readonly Lazy<Location> playerLocationLazy;

    private Scene scene = null!;

    public NPCScript(ITagContainer diContainer) : base(diContainer, CreateEntityContainer)
    {
        game = diContainer.GetTag<Game>();
        db = diContainer.GetTag<MappedDB>();
        recorder = diContainer.GetTag<EntityCommandRecorder>();
        playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
        executeScriptSubscription = World.Subscribe<messages.ExecuteNPCScript>(HandleExecuteNPCScript);
        sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
    }

    public override void Dispose()
    {
        base.Dispose();
        executeScriptSubscription.Dispose();
        sceneLoadedSubscription.Dispose();
    }

    [WithPredicate]
    private static bool IsScriptNPCState(in components.NPCState state) => state == components.NPCState.Script;

    private void HandleSceneLoaded(in messages.SceneLoaded msg) => scene = msg.Scene;

    private void HandleExecuteNPCScript(in messages.ExecuteNPCScript message)
    {
        if (message.OnlyFor == null)
        {
            var executionPool = World.GetComponents<components.ScriptExecution>();
            foreach (var entity in Set.GetEntities())
                Update(entity, ref executionPool[entity]);
        }
        else
            Update(message.OnlyFor.Value, ref message.OnlyFor.Value.Get<components.ScriptExecution>());
    }

    [Update]
    private void Update(in DefaultEcs.Entity entity, ref components.ScriptExecution execution)
    {
        Continue(entity, ref execution);
    }

    private static void SetModel(DefaultEcs.Entity entity, string name)
    {
        if (entity.TryGet<components.ActorParts>(out var oldActorParts))
        {
            oldActorParts.Body.Dispose();
            oldActorParts.Wings?.Dispose();
        }

        entity.Set(ManagedResource<ActorExDescription>.Create(name));

        var actorParts = entity.Get<components.ActorParts>();
        var bodyClump = actorParts.Body.Get<ClumpMesh>();
        var bodyHeight = bodyClump.BoundingBox.Size.Y;
        var colliderSize = entity.Get<components.NPCType>() switch
        {
            components.NPCType.Flying => FlyingColliderSize,
            components.NPCType.Item => bodyHeight + ItemColliderSizeOffset,
            _ => bodyHeight
        };
        entity.Set(new Sphere(Vector3.Zero, colliderSize));
        entity.Set(new components.NonFairyAnimation(Random.Shared));
        entity.Set<components.PuppetActorMovement>();
        entity.Set(components.FindActorFloorCollisions.Default);
        entity.Set(components.ActorLighting.Default);
    }

    private void SetCamera(DefaultEcs.Entity entity, int triggerArg)
    {
        World.Publish(new messages.SetCameraMode(3000 + triggerArg, entity));
    }

    private static void Wizform(DefaultEcs.Entity entity, int atIndex, int fairyId, int level)
    {
        var inventory = entity.Get<Inventory>();
        var invFairy = inventory.AddFairy(fairyId);
        var xp = inventory.GetXPForLevel(invFairy, (uint)level);
        inventory.AddXP(invFairy, xp);
        inventory.SetSlot(invFairy, atIndex);

        if (entity.Get<components.NPCType>() == components.NPCType.Flying &&
            !entity.Has<ActorExDescription>())
        {
            entity.Remove<Inventory>();
            entity.Set(inventory); // this forces the added fairy to spawn
            var fairyEntity = entity.Get<components.SpawnedFairy>().Entity;
            fairyEntity.Set(components.FairyHoverState.KeepLastHover);

            // this command acts as SetModel for flying NPC types
            entity.Set(new Sphere(Vector3.Zero, FlyingColliderSize));
            entity.Set<components.PuppetActorMovement>();
        }
    }

    private static void Spell(DefaultEcs.Entity entity, int fairyI, int slotI, int spellId)
    {
        var inventory = entity.Get<Inventory>();
        var invFairy = inventory.GetFairyAtSlot(fairyI) ??
            throw new ArgumentException($"Cannot set NPC spell as there is no wizform in slot {fairyI}");
        var invSpell = inventory.AddSpell(spellId);
        inventory.SetSpellSlot(invFairy, invSpell, slotI);
    }

    private void ChangeWaypoint(DefaultEcs.Entity entity, int fromWpId, int toWpId)
    {
        World.Publish(new messages.NPCChangeWaypoint(entity, fromWpId, toWpId));
        entity.Set(components.NPCState.Waypoint);
    }

    private static void LookAtPlayer(DefaultEcs.Entity entity, int intDuration, components.NPCLookAtPlayer.Mode mode)
    {
        intDuration *= 100;
        float actualDuration = intDuration == 0 || intDuration > 20000
            ? float.PositiveInfinity
            : intDuration * 0.001f;
        entity.Set(new components.NPCLookAtPlayer(mode, actualDuration));
        entity.Set(components.NPCState.LookAtPlayer);

        var nonFairyAnim = entity.TryGet<components.NonFairyAnimation>();
        if (nonFairyAnim.HasValue)
        {
            if (nonFairyAnim.Value.Next is not (AnimationType.Idle0 or AnimationType.Idle1 or AnimationType.Idle2))
                nonFairyAnim.Value.Next = AnimationType.Idle0;
        }
        // the alternative (e.g. fairy animation just does not need this Idle0 switch)
    }

    private void RemoveNPC(DefaultEcs.Entity entity)
    {
        // Unused in the original game
        World.Publish(new messages.UnreserveNextWaypoint(entity));
        World.Publish(new GSModDisableTrigger(entity.Get<Trigger>().idx));
        recorder.Record(entity).Dispose();
    }

    private bool IfTriggerIsActive(DefaultEcs.Entity entity, int triggerI)
    {
        return World
            .GetEntities()
            .With<components.ActiveTrigger>()
            .With((in Trigger t) => t.idx == triggerI)
            .AsEnumerable()
            .Any();
    }

    private bool IfTriggerIsEnabled(DefaultEcs.Entity entity, int triggerI)
    {
        logger.Debug("Warning unimplemented NPC instruction \"ifTriggerIsEnabled\"");
        return false;
    }

    private void MoveSystem(DefaultEcs.Entity entity, messages.NPCMoveSystem.Mode mode, int wpCategory)
    {
        World.Publish(new messages.NPCMoveSystem(entity, mode, wpCategory));
    }

    private static void MovementSpeed(DefaultEcs.Entity entity, int speed)
    {
        entity.Get<components.NPCMovement>().Speed = speed * 0.1f;
    }

    private void LockUserInput(DefaultEcs.Entity entity, bool isLocked)
    {
        // Unused in the original game
        if (isLocked)
        {
            World.Publish(messages.LockPlayerControl.Forever);
            World.Publish(new messages.ResetPlayerMovement());
        }
        else
            World.Publish(messages.LockPlayerControl.Unlock);
    }

    private static void PlayAnimation(DefaultEcs.Entity entity, zzio.AnimationType animationType, int intDuration)
    {
        intDuration *= 100;
        float actualDuration = intDuration == 0 || intDuration > 20000
            ? float.PositiveInfinity
            : intDuration * 0.001f;
        entity.Set(new components.NPCIdle() { TimeLeft = actualDuration });
        entity.Set(components.NPCState.Idle);

        ref var anim = ref entity.Get<components.NonFairyAnimation>();
        anim.Next = animationType;
    }

    private void StartPrelude(DefaultEcs.Entity entity)
    {
        if (game.PlayerEntity.Get<components.GameFlow>() == components.GameFlow.Normal)
            World.Publish(new messages.StartDialog(entity, components.DialogCause.Trigger));
    }

    private static void SetNPCType(DefaultEcs.Entity entity, components.NPCType type)
    {
        entity.Set(type);
    }

    private void DeployMeAtTrigger(DefaultEcs.Entity entity, int triggerI)
    {
        World.Publish(new messages.CreaturePlaceToTrigger(entity, triggerI));
    }

    private void DeployPlayerAtTrigger(DefaultEcs.Entity entity, int triggerI)
    {
        World.Publish(new messages.CreaturePlaceToTrigger(game.PlayerEntity, triggerI));
    }

    private static bool IfCloseToWaypoint(DefaultEcs.Entity entity, int waypointI)
    {
        ref readonly var move = ref entity.Get<components.NPCMovement>();
        return move.CurWaypointId == waypointI;
    }

    private static bool IfNPCModifierHasValue(DefaultEcs.Entity entity, int value)
    {
        return entity.Get<components.NPCModifier>().Value == value;
    }

    private void SetNPCModifier(DefaultEcs.Entity entity, int scene, int optTriggerI, int value)
    {
        uint triggerI = optTriggerI < 0
            ? entity.Get<Trigger>().idx
            : (uint)optTriggerI;
        var gsmod = new GSModSetNPCModifier(triggerI, value);

        if (scene < 0)
            World.Publish(gsmod);
        else
            World.Publish(new messages.GSModForScene(scene, gsmod));
    }

    private void DefaultWizform(DefaultEcs.Entity entity, int fairyId, int groupOrSlotI, int level)
    {
        if (fairyId < 0)
        {
            (var stdFairyId, level) = StdFairyGroups.GetFromAttackGroup(Random.Shared, groupOrSlotI);
            fairyId = (int)stdFairyId;
            groupOrSlotI = 0;
        }
        Wizform(entity, groupOrSlotI, fairyId, level);

        var inventory = entity.Get<Inventory>();
        var invFairy = inventory.GetFairyAtSlot(groupOrSlotI)!;
        var dbFairy = db.GetFairy(invFairy.dbUID);
        var spellSet = StdSpells.GetRandomSpellSet(db, Random.Shared, dbFairy.Class0, invFairy.level);
        ApplySpell(0, spellSet.attack0);
        ApplySpell(1, spellSet.support0);
        ApplySpell(2, spellSet.attack1);
        ApplySpell(3, spellSet.support1);

        void ApplySpell(int slotI, SpellRow? dbSpell)
        {
            if (dbSpell == null)
                return;
            var invSpell = inventory.AddSpell(dbSpell.CardId.EntityId);
            inventory.SetSpellSlot(invFairy, invSpell, slotI);
        }
    }

    private void DefaultDeck(DefaultEcs.Entity entity, int groupI, int levelRange)
    {
        var deck = StdFairyGroups.GetFromDeck(Random.Shared, groupI, levelRange);
        foreach (var ((fairyId, level), slotI) in deck.Indexed())
            DefaultWizform(entity, (int)fairyId, slotI, level);
    }

    private static void Idle(DefaultEcs.Entity entity)
    {
        entity.Set(components.NPCIdle.Default);
        entity.Set(components.NPCState.Idle);
    }

    private bool IfPlayerIsClose(DefaultEcs.Entity entity, int maxDistSqr)
    {
        // TODO: Check the gameflow state in NPC IfPlayerIsClose
        var location = entity.Get<Location>();
        return Vector3.DistanceSquared(location.LocalPosition, playerLocation.LocalPosition) < maxDistSqr;
    }

    private static void SetCollision(DefaultEcs.Entity entity, bool isSolid)
    {
        if (isSolid)
            entity.Set<components.Collidable>();
        else
            entity.Remove<components.Collidable>();
    }

    private void CreateDynamicItems(DefaultEcs.Entity entity, int itemId, int count, int triggerI)
    {
        if (triggerI >= scene.triggers.Length)
            throw new ArgumentOutOfRangeException(nameof(triggerI), $"Invalid trigger index for CreateDynamicItems");
        var position = triggerI < 0
            ? entity.Get<Location>().LocalPosition
            : scene.triggers[triggerI].pos;
        World.Publish(new messages.CreateItem(itemId, position, count));
    }

    private static void Revive(DefaultEcs.Entity entity) => entity.Get<Inventory>().FillMana();

    private static void LookAtTrigger(DefaultEcs.Entity entity, int intDuration, int triggerI)
    {
        float actualDuration = intDuration == 0 || intDuration > 20000
            ? float.PositiveInfinity
            : intDuration * 0.001f;
        entity.Set(new components.NPCLookAtTrigger(triggerI, actualDuration));
        entity.Set(components.NPCState.LookAtTrigger);
    }

    private void PlaySound(DefaultEcs.Entity entity, int soundId)
    {
        LogUnimplementedInstructionWarning();
    }

    private void StartActorEffect(DefaultEcs.Entity entity, int effectId)
    {
        LogUnimplementedInstructionWarning();
    }

    private void EndActorEffect(DefaultEcs.Entity entity)
    {
        LogUnimplementedInstructionWarning();
    }
}
