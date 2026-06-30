using System;
using DefaultEcs.System;

namespace zzre.game.systems;

public class DialogLookAt : ISystem<float>
{
    public bool IsEnabled { get; set; } = true;

    private readonly DefaultEcs.World ecsWorld;
    private readonly IDisposable addedSubscription;
    private readonly IDisposable removedSubscription;
    private readonly IDisposable changedSubscription;

    public DialogLookAt(ITagContainer diContainer)
    {
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        addedSubscription = ecsWorld.SubscribeEntityComponentAdded<components.DialogState>(HandleAddedComponent);
        removedSubscription = ecsWorld.SubscribeEntityComponentRemoved<components.DialogState>(HandleRemovedComponent);
        changedSubscription = ecsWorld.SubscribeEntityComponentChanged<components.DialogState>(HandleChangedComponent);
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
        HandlePlayerLookAt(dialogEntity, false);
        HandleNPCLookAt(dialogEntity, false);
    }

    private void HandleChangedComponent(in DefaultEcs.Entity dialogEntity, in components.DialogState oldValue, in components.DialogState newValue)
    {
        if (ShouldPlayerLookAt(newValue) != ShouldPlayerLookAt(oldValue))
            HandlePlayerLookAt(dialogEntity, ShouldPlayerLookAt(newValue));
        if (ShouldNPCLookAt(newValue) != ShouldNPCLookAt(oldValue))
            HandleNPCLookAt(dialogEntity, ShouldNPCLookAt(newValue));
    }

    private static bool ShouldPlayerLookAt(components.DialogState state) =>
        ShouldNPCLookAt(state) ||
        state == components.DialogState.NpcWalking;

    private void HandlePlayerLookAt(DefaultEcs.Entity dialogEntity, bool isEnabled)
    {
        var playerEntity = ecsWorld.Get<components.PlayerEntity>().Entity;
        if (isEnabled && dialogEntity.IsAlive)
        {
            var npcLocation = dialogEntity.Get<components.DialogNPC>().Entity.Get<Location>();
            playerEntity.Set(new components.PuppetActorTarget(npcLocation));
        }
        else
            playerEntity.Remove<components.PuppetActorTarget>();
    }

    private static bool ShouldNPCLookAt(components.DialogState state) =>
        state == components.DialogState.Delay ||
        state == components.DialogState.WaitForSayString ||
        state == components.DialogState.Talk ||
        state == components.DialogState.Choice;

    private void HandleNPCLookAt(DefaultEcs.Entity dialogEntity, bool isEnabled)
    {
        var npcEntity = dialogEntity.Get<components.DialogNPC>().Entity;
        if (!npcEntity.IsAlive)
            return;

        var playerLocation = ecsWorld.Get<components.PlayerEntity>().Entity.Get<Location>();
        if (isEnabled && npcEntity.Get<components.NPCType>() == components.NPCType.Biped) // TODO: Fix Fairy NPCs not looking at player during dialogs
            npcEntity.Set(new components.PuppetActorTarget(playerLocation));
        else
            npcEntity.Remove<components.PuppetActorTarget>();
    }

    public void Update(float state)
    {
    }
}
