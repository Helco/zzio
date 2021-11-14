using System;
using System.Numerics;
using DefaultEcs.System;
using DefaultEcs.Resource;
using zzio.script;
using System.Linq;

namespace zzre.game.systems
{
    public partial class NPCScript : BaseScript
    {
        private const float FlyingColliderSize = 0.2f;
        private const float ItemColliderSizeOffset = -0.05f;

        private readonly IDisposable executeScriptSubscription;
        private Location playerLocation => playerLocationLazy.Value;
        private readonly Lazy<Location> playerLocationLazy;

        public NPCScript(ITagContainer diContainer) : base(diContainer, CreateEntityContainer2)
        {
            var game = diContainer.GetTag<Game>();
            playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
            executeScriptSubscription = World.Subscribe<messages.ExecuteNPCScript>(HandleExecuteNPCScript);
        }

        public override void Dispose()
        {
            base.Dispose();
            executeScriptSubscription.Dispose();
        }

        private static DefaultEcs.EntitySet CreateEntityContainer2(object sender, DefaultEcs.World world) => world
            .GetEntities()
            .With<components.ScriptExecution>()
            .With<components.NPCState>(IsScriptNPCState)
            .AsSet();

        private static bool IsScriptNPCState(in components.NPCState state) => state == components.NPCState.Script;

        private void HandleExecuteNPCScript(in messages.ExecuteNPCScript message)
        {
            var executionPool = World.GetComponents<components.ScriptExecution>();
            foreach (var entity in Set.GetEntities())
                Update(entity, ref executionPool[entity]);
        }

        protected override void Update(float state, in DefaultEcs.Entity entity) => Update(entity, ref entity.Get<components.ScriptExecution>());

        private void Update(in DefaultEcs.Entity entity, ref components.ScriptExecution execution)
        {
            Continue(entity, ref execution);
        }

        // TODO: Missing NPC script instructions

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
        }

        private void SetCamera(DefaultEcs.Entity entity, int triggerArg)
        {
            Console.WriteLine("Warning: unimplemented instruction \"SetCamera\"");
        }

        private void Wizform(DefaultEcs.Entity entity, int atIndex, int fairyId, int level)
        {
            Console.WriteLine("Warning: unimplemented instruction \"Wizform\"");
        }

        private void Spell(DefaultEcs.Entity entity, int fairyI, int slotI, int spellId)
        {
            Console.WriteLine("Warning: unimplemented instruction \"Spell\"");
        }

        private void ChangeWaypoint(DefaultEcs.Entity entity, int fromWpId, int toWpId)
        {
            Console.WriteLine("Warning: unimplemented instruction \"ChangeWaypoint\"");
        }

        private void LookAtPlayer(DefaultEcs.Entity entity, int duration, int mode)
        {
            Console.WriteLine("Warning: unimplemented instruction \"LookAtPlayer\"");
        }

        private void RemoveNPC(DefaultEcs.Entity entity)
        {
            Console.WriteLine("Warning: unimplemented instruction \"RemoveNPC\"");
        }

        private bool IfTriggerIsActive(DefaultEcs.Entity entity, int triggerI)
        {
            Console.WriteLine("Warning: unimplemented instruction \"IfTriggerIsActive\"");
            return false;
        }

        private void MoveSystem(DefaultEcs.Entity entity, int mode, int wpCategory)
        {
            Console.WriteLine("Warning: unimplemented instruction \"MoveSystem\"");
        }

        private void MovementSpeed(DefaultEcs.Entity entity, int speed)
        {
            Console.WriteLine("Warning: unimplemented instruction \"MovementSpeed\"");
        }

        private void LockUserInput(DefaultEcs.Entity entity, int isLocked)
        {
            Console.WriteLine("Warning: unimplemented instruction \"LockUserInput\"");
        }

        private void PlayAnimation(DefaultEcs.Entity entity, zzio.AnimationType animationType, int duration)
        {
            Console.WriteLine("Warning: unimplemented instruction \"PlayAnimation\"");
        }

        private void StartPrelude(DefaultEcs.Entity entity)
        {
            Console.WriteLine("Warning: unimplemented instruction \"StartPrelude\"");
        }

        private void SetNPCType(DefaultEcs.Entity entity, components.NPCType type)
        {
            entity.Set(type);
        }

        private void DeployMeAtTrigger(DefaultEcs.Entity entity, int triggerI)
        {
            Console.WriteLine("Warning: unimplemented instruction \"DeployMeAtTrigger\"");
        }

        private void DeployPlayerAtTrigger(DefaultEcs.Entity entity, int triggerI)
        {
            Console.WriteLine("Warning: unimplemented instruction \"DeployPlayerAtTrigger\"");
        }

        private void DeployNPCAtTrigger(DefaultEcs.Entity entity, zzio.primitives.UID uid)
        {
            Console.WriteLine("Warning: unimplemented instruction \"DeployNPCAtTrigger\"");
        }

        private bool IfCloseToWaypoint(DefaultEcs.Entity entity, int waypointI)
        {
            Console.WriteLine("Warning: unimplemented instruction \"IfCloseToWaypoint\"");
            return false;
        }

        private bool IfNPCModifierHasValue(DefaultEcs.Entity entity, int value)
        {
            return entity.Get<components.NPCModifier>().Value == value;
        }

        private void SetNPCModifier(DefaultEcs.Entity entity, int scene, int triggerI, int value)
        {
            Console.WriteLine("Warning: unimplemented instruction \"SetNPCModifier\"");
        }

        private void DefaultWizform(DefaultEcs.Entity entity, int fairyId, int groupOrSlotI, int level)
        {
            Console.WriteLine("Warning: unimplemented instruction \"DefaultWizform\"");
        }

        private void Idle(DefaultEcs.Entity entity)
        {
            Console.WriteLine("Warning: unimplemented instruction \"Idle\"");
        }

        private bool IfPlayerIsClose(DefaultEcs.Entity entity, int maxDistSqr)
        {
            Console.WriteLine("Warning: unimplemented instruction \"IfPlayerIsClose\"");
            return false;
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
            Console.WriteLine("Warning: unimplemented instruction \"CreateDynamicItems\"");
        }

        private void Revive(DefaultEcs.Entity entity)
        {
            Console.WriteLine("Warning: unimplemented instruction \"Revive\"");
        }

        private void LookAtTrigger(DefaultEcs.Entity entity, int duration, int triggerI)
        {
            Console.WriteLine("Warning: unimplemented instruction \"LookAtTrigger\"");
        }
    }
}
