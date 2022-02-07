using System;
using System.Linq;
using DefaultEcs.System;
using zzio;
using zzio.script;

namespace zzre.game.systems
{
    public partial class DialogScript : BaseScript
    {
        public enum SpecialInventoryCheck
        {
            HasFivePixies = 0,
            HasAFairy,
            HasAtLeastNFairies,
            HasFairyOfClass
        }

        public enum SceneObjectType
        {
            Platforms = 0,
            Items
        }

        public enum SubGameType
        {
            ChestPuzzle = 0,
            ElfGame
        }

        public DialogScript(ITagContainer diContainer) : base(diContainer, CreateEntityContainer)
        {
        }

        [Update]
        private void Update(in DefaultEcs.Entity entity, ref components.ScriptExecution execution)
        {
            // *do* we want to continue?
        }

        private void Say(DefaultEcs.Entity entity, UID uid, bool silent)
        {
            throw new NotImplementedException();
        }

        private void Choice(DefaultEcs.Entity entity, int targetLabel, UID uid)
        {
            throw new NotImplementedException();
        }

        private void WaitForUser(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private void SetCamera(DefaultEcs.Entity entity, int triggerArg)
        {
            throw new NotImplementedException();
        }

        private void ChangeWaypoint(DefaultEcs.Entity entity, int fromWpId, int toWpId)
        {
            throw new NotImplementedException();
        }

        private void Fight(DefaultEcs.Entity entity, int stage, bool canFlee)
        {
            throw new NotImplementedException();
        }

        private void ChangeDatabase(DefaultEcs.Entity entity, UID uid)
        {
            throw new NotImplementedException();
        }

        private void RemoveNpc(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private void CatchWizform(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private void KillPlayer(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private void TradingCurrency(DefaultEcs.Entity entity, UID uid)
        {
            throw new NotImplementedException();
        }

        private void TradingCard(DefaultEcs.Entity entity, int price, UID uid)
        {
            throw new NotImplementedException();
        }

        private void GivePlayerCards(DefaultEcs.Entity entity, int count, int type, int id)
        {
            throw new NotImplementedException();
        }

        private void SetupGambling(DefaultEcs.Entity entity, int count, int type, int id)
        {
            throw new NotImplementedException();
        }

        private bool IfPlayerHasCards(DefaultEcs.Entity entity, int count, int type, int id)
        {
            throw new NotImplementedException();
        }

        private bool IfPlayerHasSpecials(DefaultEcs.Entity entity, SpecialInventoryCheck specialType, int arg)
        {
            throw new NotImplementedException();
        }

        private bool IfTriggerIsActive(DefaultEcs.Entity entity, int triggerI)
        {
            throw new NotImplementedException();
        }

        private void RemovePlayerCards(DefaultEcs.Entity entity, int count, int type, int id)
        {
            throw new NotImplementedException();
        }

        private void LockUserInput(DefaultEcs.Entity entity, int mode)
        {
            throw new NotImplementedException();
        }

        private void ModifyTrigger(DefaultEcs.Entity entity, int enableTrigger, int id, int triggerI)
        {
            throw new NotImplementedException();
        }

        private void PlayAnimation(DefaultEcs.Entity entity, AnimationType animation)
        {
            throw new NotImplementedException();
        }

        private void NpcWizformEscapes(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private void Talk(DefaultEcs.Entity entity, UID uid)
        {
            throw new NotImplementedException();
        }

        private void ChafferWizforms(UID uid, UID uid2, UID uid3)
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

        private void DeployNPCAtTrigger(DefaultEcs.Entity entity, UID uid)
        {
            throw new NotImplementedException();
        }

        private void Delay(DefaultEcs.Entity entity, int duration)
        {
            throw new NotImplementedException();
        }

        private void RemoveWizforms(DefaultEcs.Entity entity)
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

        private bool IfPlayerIsClose(DefaultEcs.Entity entity, int maxDistSqr)
        {
            throw new NotImplementedException();
        }

        private bool IfNumberOfNpcsIs(DefaultEcs.Entity entity, int count, UID uid)
        {
            throw new NotImplementedException();
        }

        private void StartEffect(DefaultEcs.Entity entity, int effectType, int triggerI)
        {
            throw new NotImplementedException();
        }

        private void SetTalkLabels(DefaultEcs.Entity entity, int labelYes, int labelNo, int mode)
        {
            throw new NotImplementedException();
        }

        private void TradeWizform(DefaultEcs.Entity entity, int id)
        {
            throw new NotImplementedException();
        }

        private void CreateDynamicItems(DefaultEcs.Entity entity, int id, int count, int triggerI)
        {
            throw new NotImplementedException();
        }

        private void PlayVideo(DefaultEcs.Entity entity, int id)
        {
            throw new NotImplementedException();
        }

        private void RemoveNpcAtTrigger(int triggerI)
        {
            throw new NotImplementedException();
        }

        private void Revive(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private bool IfTriggerIsEnabled(int triggerI)
        {
            throw new NotImplementedException();
        }

        private void PlaySound(DefaultEcs.Entity entity, int id)
        {
            throw new NotImplementedException();
        }

        private void PlayInArena(DefaultEcs.Entity entity, int arg)
        {
            throw new NotImplementedException();
        }

        private void EndActorEffect(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private void CreateSceneObjects(DefaultEcs.Entity entity, SceneObjectType objectType)
        {
            throw new NotImplementedException();
        }

        private void RemoveBehavior(DefaultEcs.Entity entity, int id)
        {
            throw new NotImplementedException();
        }

        private void UnlockDoor(DefaultEcs.Entity entity, int id, bool isMetalDoor)
        {
            throw new NotImplementedException();
        }

        private void EndGame(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private void SubGame(DefaultEcs.Entity entity, SubGameType subGameType, int size, int labelExit)
        {
            throw new NotImplementedException();
        }

        private void PlayPlayerAnimation(DefaultEcs.Entity entity, AnimationType animation)
        {
            throw new NotImplementedException();
        }

        private void PlayAmyVoice(string v)
        {
            throw new NotImplementedException();
        }

        private void CreateDynamicModel(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }

        private void DeploySound(DefaultEcs.Entity entity, int id, int triggerI)
        {
            throw new NotImplementedException();
        }

        private void GivePlayerPresent(DefaultEcs.Entity entity)
        {
            throw new NotImplementedException();
        }
    }
}
