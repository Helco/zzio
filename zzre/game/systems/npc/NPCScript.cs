using System;
using System.Numerics;
using DefaultEcs.System;
using DefaultEcs.Resource;
using zzio.script;
using System.Linq;

namespace zzre.game.systems
{
    [With(typeof(components.NPC))]
    public partial class NPCScript : BaseScript
    {
        private readonly IDisposable executeScriptSubscription;
        private Location playerLocation => playerLocationLazy.Value;
        private readonly Lazy<Location> playerLocationLazy;

        public NPCScript(ITagContainer diContainer) : base(diContainer, CreateEntityContainer)
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

        // TODO: Missing NPC script instructions

        private void SetModel(DefaultEcs.Entity entity, string v)
        {
            throw new NotImplementedException();
        }

        private void SetCamera(DefaultEcs.Entity entity, int triggerArg)
        {
            throw new NotImplementedException();
        }

        private void Wizform(DefaultEcs.Entity entity, int atIndex, int fairyId, int level)
        {
            throw new NotImplementedException();
        }

        private void Spell(DefaultEcs.Entity entity, int fairyI, int slotI, int spellId)
        {
            throw new NotImplementedException();
        }

        private void ChangeWaypoint(DefaultEcs.Entity entity, int fromWpId, int toWpId)
        {
            throw new NotImplementedException();
        }

        private void LookAtPlayer(DefaultEcs.Entity entity, int duration, int mode)
        {
            throw new NotImplementedException();
        }

        private void RemoveNPC(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private bool IfTriggerIsActive(DefaultEcs.Entity entity, int triggerI)
        {
            throw new NotImplementedException();
        }

        private void MoveSystem(DefaultEcs.Entity entity, int mode, int wpCategory)
        {
            throw new NotImplementedException();
        }

        private void MovementSpeed(DefaultEcs.Entity entity, int speed)
        {
            throw new NotImplementedException();
        }

        private void LockUserInput(DefaultEcs.Entity entity, int isLocked)
        {
            throw new NotImplementedException();
        }

        private void PlayAnimation(DefaultEcs.Entity entity, zzio.AnimationType animationType, int duration)
        {
            throw new NotImplementedException();
        }

        private void StartPrelude(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private void SetNPCType(DefaultEcs.Entity entity, int type)
        {
            throw new NotImplementedException();
        }

        private void DeployMeAtTrigger(DefaultEcs.Entity entity, int triggerI)
        {
            throw new NotImplementedException();
        }

        private void DeployPlayerAtTrigger(DefaultEcs.Entity entity, int triggerI)
        {
            throw new NotImplementedException();
        }

        private void DeployNPCAtTrigger(DefaultEcs.Entity entity, zzio.primitives.UID uid)
        {
            throw new NotImplementedException();
        }

        private bool IfCloseToWaypoint(DefaultEcs.Entity entity, int waypointI)
        {
            throw new NotImplementedException();
        }

        private bool IfNPCModifierHasValue(DefaultEcs.Entity entity, int value)
        {
            throw new NotImplementedException();
        }

        private void SetNPCModifier(DefaultEcs.Entity entity, int scene, int triggerI, int value)
        {
            throw new NotImplementedException();
        }

        private void DefaultWizform(DefaultEcs.Entity entity, int fairyId, int groupOrSlotI, int level)
        {
            throw new NotImplementedException();
        }

        private void Idle(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private bool IfPlayerIsClose(DefaultEcs.Entity entity, int maxDistSqr)
        {
            throw new NotImplementedException();
        }

        private void SetCollision(DefaultEcs.Entity entity, bool isSolid)
        {
            throw new NotImplementedException();
        }

        private void CreateDynamicItems(DefaultEcs.Entity entity, int itemId, int count, int triggerI)
        {
            throw new NotImplementedException();
        }

        private void Revive(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private void LookAtTrigger(DefaultEcs.Entity entity, int duration, int triggerI)
        {
            throw new NotImplementedException();
        }
    }
}
