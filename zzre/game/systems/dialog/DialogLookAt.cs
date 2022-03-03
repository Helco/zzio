using System;
using DefaultEcs.System;

namespace zzre.game.systems
{
    public class DialogLookAt : ISystem<float>
    {
        public bool IsEnabled { get; set; } = true;

        private readonly Game game;
        private readonly IDisposable addedSubscription;
        private readonly IDisposable removedSubscription;
        private readonly IDisposable changedSubscription;

        public DialogLookAt(ITagContainer diContainer)
        {
            game = diContainer.GetTag<Game>();
            var world = diContainer.GetTag<DefaultEcs.World>();
            addedSubscription = world.SubscribeComponentAdded<components.DialogState>(HandleAddedComponent);
            removedSubscription = world.SubscribeComponentRemoved<components.DialogState>(HandleRemovedComponent);
            changedSubscription = world.SubscribeComponentChanged<components.DialogState>(HandleChangedComponent);
        }
        public void Dispose()
        {
            addedSubscription.Dispose();
            removedSubscription.Dispose();
            changedSubscription.Dispose();
        }

        private void HandleAddedComponent(in DefaultEcs.Entity dialogEntity, in components.DialogState value)
        {
            if (ShouldPlayerLookAt(value))
                HandlePlayerLookAt(dialogEntity, true);
            if (ShouldNPCLookAt(value))
                HandleNPCLookAt(dialogEntity, true);
        }

        private void HandleRemovedComponent(in DefaultEcs.Entity dialogEntity, in components.DialogState value)
        {
            if (ShouldPlayerLookAt(value))
                HandlePlayerLookAt(dialogEntity, false);
            if (ShouldNPCLookAt(value))
                HandleNPCLookAt(dialogEntity, false);
        }

        private void HandleChangedComponent(in DefaultEcs.Entity dialogEntity, in components.DialogState oldValue, in components.DialogState newValue)
        {
            if (ShouldPlayerLookAt(newValue) != ShouldPlayerLookAt(oldValue))
                HandlePlayerLookAt(dialogEntity, ShouldPlayerLookAt(newValue));
            if (ShouldNPCLookAt(newValue) != ShouldNPCLookAt(oldValue))
                HandleNPCLookAt(dialogEntity, ShouldNPCLookAt(newValue));
        }

        private bool ShouldPlayerLookAt(components.DialogState state) =>
            ShouldNPCLookAt(state) ||
            state == components.DialogState.NpcWalking;

        private void HandlePlayerLookAt(DefaultEcs.Entity dialogEntity, bool isEnabled)
        {
            if (isEnabled)
            {
                var npcLocation = dialogEntity.Get<components.DialogNPC>().Entity.Get<Location>();
                game.PlayerEntity.Set(new components.PuppetActorTarget(npcLocation));
            }
            else
                game.PlayerEntity.Remove<components.PuppetActorTarget>();
        }

        private bool ShouldNPCLookAt(components.DialogState state) =>
            state == components.DialogState.Delay ||
            state == components.DialogState.WaitForSayString ||
            state == components.DialogState.Talk ||
            state == components.DialogState.Choice;

        private void HandleNPCLookAt(DefaultEcs.Entity dialogEntity, bool isEnabled)
        {
            var npcEntity = dialogEntity.Get<components.DialogNPC>().Entity;
            if (isEnabled)
                npcEntity.Set(new components.PuppetActorTarget(game.PlayerEntity.Get<Location>()));
            else
                npcEntity.Remove<components.PuppetActorTarget>();
        }

        public void Update(float state)
        {
        }
    }
}
