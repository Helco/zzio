using System;
using System.Numerics;
using DefaultEcs.System;
using DefaultEcs.Resource;
using System.Linq;
using zzio.scn;
using zzio;

namespace zzre.game.systems
{
    [PauseDuring(PauseTrigger.UIScreen)]
    public partial class NPCScript : BaseScript
    {
        private const float FlyingColliderSize = 0.2f;
        private const float ItemColliderSizeOffset = -0.05f;

        private readonly IDisposable executeScriptSubscription;
        private readonly Game game;
        private readonly Scene scene;
        private Location playerLocation => playerLocationLazy.Value;
        private readonly Lazy<Location> playerLocationLazy;

        public NPCScript(ITagContainer diContainer) : base(diContainer, CreateEntityContainer)
        {
            game = diContainer.GetTag<Game>();
            scene = diContainer.GetTag<Scene>();
            playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
            executeScriptSubscription = World.Subscribe<messages.ExecuteNPCScript>(HandleExecuteNPCScript);
        }

        public override void Dispose()
        {
            base.Dispose();
            executeScriptSubscription.Dispose();
        }

        [WithPredicate]
        private static bool IsScriptNPCState(in components.NPCState state) => state == components.NPCState.Script;

        private void HandleExecuteNPCScript(in messages.ExecuteNPCScript message)
        {
            var executionPool = World.GetComponents<components.ScriptExecution>();
            foreach (var entity in Set.GetEntities())
                Update(entity, ref executionPool[entity]);
        }

        [Update]
        private void Update(in DefaultEcs.Entity entity, ref components.ScriptExecution execution)
        {
            Continue(entity, ref execution);
        }

        // TODO: Missing NPC script NPC instructions

        private void SetModel(DefaultEcs.Entity entity, string name)
        {
            if (entity.Has<components.ActorParts>()) // not required by Zanzarah and a bit too much effort
                throw new InvalidOperationException("NPC already has a model");
            entity.Set(ManagedResource<zzio.ActorExDescription>.Create(name));

            // type is already set at this point if it is set at all
            var npcType = entity.Has<components.NPCType>()
                ? entity.Get<components.NPCType>()
                : default;
            var actorParts = entity.Get<components.ActorParts>();
            var bodyClump = actorParts.Body.Get<ClumpBuffers>();
            var bodyHeight = bodyClump.Bounds.Size.Y;
            var colliderSize = npcType switch
            {
                components.NPCType.Flying => FlyingColliderSize,
                components.NPCType.Item => bodyHeight + ItemColliderSizeOffset,
                _ => bodyHeight
            };
            entity.Set(new Sphere(Vector3.Zero, colliderSize));
            entity.Set(new components.NonFairyAnimation(GlobalRandom.Get));
            entity.Set<components.PuppetActorMovement>();
        }

        private void SetCamera(DefaultEcs.Entity entity, int triggerArg)
        {
            Console.WriteLine("Warning: unimplemented NPC instruction \"SetCamera\"");
        }

        private void Wizform(DefaultEcs.Entity entity, int atIndex, int fairyId, int level)
        {
            Console.WriteLine("Warning: unimplemented NPC instruction \"Wizform\"");
        }

        private void Spell(DefaultEcs.Entity entity, int fairyI, int slotI, int spellId)
        {
            Console.WriteLine("Warning: unimplemented NPC instruction \"Spell\"");
        }

        private void ChangeWaypoint(DefaultEcs.Entity entity, int fromWpId, int toWpId)
        {
            World.Publish(new messages.NPCChangeWaypoint(entity, fromWpId, toWpId));
            entity.Set(components.NPCState.Waypoint);
        }

        private void LookAtPlayer(DefaultEcs.Entity entity, int intDuration, components.NPCLookAtPlayer.Mode mode)
        {
            intDuration *= 100;
            float actualDuration = intDuration == 0 || intDuration > 20000
                ? float.PositiveInfinity
                : intDuration * 0.001f;
            entity.Set(new components.NPCLookAtPlayer(mode, actualDuration));
            entity.Set(components.NPCState.LookAtPlayer);

            ref var anim = ref entity.Get<components.NonFairyAnimation>();
            if (anim.Next != zzio.AnimationType.Idle0 &&
                anim.Next != zzio.AnimationType.Idle1 &&
                anim.Next != zzio.AnimationType.Idle2)
                anim.Next = zzio.AnimationType.Idle0;
        }

        private void RemoveNPC(DefaultEcs.Entity entity)
        {
            Console.WriteLine("Warning: unimplemented NPC instruction \"RemoveNPC\"");
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

        private void MoveSystem(DefaultEcs.Entity entity, messages.NPCMoveSystem.Mode mode, int wpCategory)
        {
            World.Publish(new messages.NPCMoveSystem(entity, mode, wpCategory));
        }

        private void MovementSpeed(DefaultEcs.Entity entity, int speed)
        {
            entity.Get<components.NPCMovement>().Speed = speed * 0.1f;
        }

        private void LockUserInput(DefaultEcs.Entity entity, int isLocked)
        {
            Console.WriteLine("Warning: unimplemented NPC instruction \"LockUserInput\"");
        }

        private void PlayAnimation(DefaultEcs.Entity entity, zzio.AnimationType animationType, int intDuration)
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
            World.Publish(new messages.StartDialog(entity, DialogCause.Trigger));
        }

        private void SetNPCType(DefaultEcs.Entity entity, components.NPCType type)
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

        private void DeployNPCAtTrigger(DefaultEcs.Entity entity, UID uid)
        {
            // In the original game, NPC scripts cannot execute this
            Console.WriteLine("Warning: unimplemented NPC instruction \"DeployNPCAtTrigger\"");
        }

        private bool IfCloseToWaypoint(DefaultEcs.Entity entity, int waypointI)
        {
            ref readonly var move = ref entity.Get<components.NPCMovement>();
            return move.CurWaypointId == waypointI;
        }

        private bool IfNPCModifierHasValue(DefaultEcs.Entity entity, int value)
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
            Console.WriteLine("Warning: unimplemented NPC instruction \"DefaultWizform\"");
        }

        private void Idle(DefaultEcs.Entity entity)
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

        private void SetCollision(DefaultEcs.Entity entity, bool isSolid)
        {
            if (isSolid)
                entity.Set<components.Collidable>();
            else
                entity.Remove<components.Collidable>();
        }

        private void CreateDynamicItems(DefaultEcs.Entity entity, int itemId, int count, int triggerI)
        {
            Console.WriteLine("Warning: unimplemented NPC instruction \"CreateDynamicItems\"");
        }

        private void Revive(DefaultEcs.Entity entity)
        {
            Console.WriteLine("Warning: unimplemented NPC instruction \"Revive\"");
        }

        private void LookAtTrigger(DefaultEcs.Entity entity, int intDuration, int triggerI)
        {
            float actualDuration = intDuration == 0 || intDuration > 20000
                ? float.PositiveInfinity
                : intDuration * 0.001f;
            entity.Set(new components.NPCLookAtTrigger(triggerI, actualDuration));
            entity.Set(components.NPCState.LookAtTrigger);
        }
    }
}
