using System;
using System.Linq;
using DefaultEcs.System;
using zzio;
using zzio.scn;

namespace zzre.game.systems;

public class Savegame : ISystem<float>
{
    public bool IsEnabled { get; set; } = true;

    private readonly DefaultEcs.World world;
    private readonly zzio.Savegame savegame;
    private readonly IDisposable sceneLoadedDisposable;
    private readonly IDisposable setTriggerExecuteDisposable;
    private readonly IDisposable playerEnteredDisposable;
    private readonly IDisposable disableAttackTriggerDisposable;
    private readonly IDisposable removeItemDisposable;
    private readonly IDisposable changeNpcStateDisposable;
    private readonly IDisposable disableTriggerDisposable;
    private readonly IDisposable removeModelDisposable;
    private readonly IDisposable setTriggerDisposable;
    private readonly IDisposable setNpcModifierDisposable;
    private readonly IDisposable gsmodForSceneDisposable;

    private uint CurSceneID => world.Get<Scene>().dataset.sceneId;

    public Savegame(ITagContainer diContainer)
    {
        world = diContainer.GetTag<DefaultEcs.World>();
        savegame = diContainer.GetTag<zzio.Savegame>();
        sceneLoadedDisposable = world.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        setTriggerExecuteDisposable = world.Subscribe<GSModSetTrigger>(HandleSetTriggerExecute);
        playerEnteredDisposable = world.Subscribe<messages.PlayerEntered>(HandlePlayerEntered);
        disableAttackTriggerDisposable = world.Subscribe<GSModDisableAttackTrigger>(HandleDisableAttackTrigger);
        removeItemDisposable = world.Subscribe<GSModRemoveItem>(HandleRemoveItem);
        changeNpcStateDisposable = world.Subscribe<GSModChangeNPCState>(HandleChangeNpcState);
        disableTriggerDisposable = world.Subscribe<GSModDisableTrigger>(HandleDisableTrigger);
        removeModelDisposable = world.Subscribe<GSModRemoveModel>(HandleRemoveModel);
        setTriggerDisposable = world.Subscribe<GSModSetTrigger>(HandleSetTrigger);
        setNpcModifierDisposable = world.Subscribe<GSModSetNPCModifier>(HandleSetNpcModifier);
        gsmodForSceneDisposable = world.Subscribe<messages.GSModForScene>(HandleGSModForScene);
    }

    public void Dispose()
    {
        sceneLoadedDisposable.Dispose();
        setTriggerExecuteDisposable.Dispose();
        playerEnteredDisposable.Dispose();
        disableAttackTriggerDisposable.Dispose();
        removeItemDisposable.Dispose();
        changeNpcStateDisposable.Dispose();
        removeModelDisposable.Dispose();
        disableTriggerDisposable.Dispose();
        setTriggerDisposable.Dispose();
        setNpcModifierDisposable.Dispose();
        gsmodForSceneDisposable.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        var setTriggers = message.GetGameState<GSModSetTrigger>();
        foreach (var set in setTriggers)
            HandleSetTriggerExecute(set);
    }

    private void HandleSetTriggerExecute(in GSModSetTrigger msg)
    {
        // this might have to be done before anything else loads in the scene
        // so this is sadly the best place for it right now
        var triggerId = msg.TriggerId;
        var trigger = world.Get<Scene>().triggers.FirstOrDefault(t => t.idx == triggerId);
        if (trigger == null)
            return;
        if (msg.II1 != uint.MaxValue) trigger.ii1 = msg.II1;
        if (msg.II2 != uint.MaxValue) trigger.ii2 = msg.II2;
        if (msg.II3 != uint.MaxValue) trigger.ii3 = msg.II3;
        if (msg.II4 != uint.MaxValue) trigger.ii4 = msg.II4;
    }

    private void HandlePlayerEntered(in messages.PlayerEntered message)
    {
        savegame.sceneId = (int)CurSceneID;
        savegame.entryId = (int)message.EntryTrigger.idx;
    }

    private void HandleDisableAttackTrigger(in GSModDisableAttackTrigger message) => HandleGSModForThisScene(message);
    private void HandleRemoveItem(in GSModRemoveItem message) => HandleGSModForThisScene(message);
    private void HandleChangeNpcState(in GSModChangeNPCState message) => HandleGSModForThisScene(message);
    private void HandleDisableTrigger(in GSModDisableTrigger message) => HandleGSModForThisScene(message);
    private void HandleRemoveModel(in GSModRemoveModel message) => HandleGSModForThisScene(message);
    private void HandleSetTrigger(in GSModSetTrigger message) => HandleGSModForThisScene(message);
    private void HandleSetNpcModifier(in GSModSetNPCModifier message) => HandleGSModForThisScene(message);

    private void HandleGSModForThisScene(IGameStateMod gsMod)
    {
        if (IsEnabled)
            savegame.Add($"sc_{CurSceneID:D4}", gsMod);
    }

    private void HandleGSModForScene(in messages.GSModForScene message)
    {
        if (message.SceneId != CurSceneID)
        {
            savegame.Add($"sc_{message.SceneId:D4}", message.Mod);
            return;
        }

        // for the current scene we still want to execute it
        switch (message.Mod)
        {
            case GSModDisableAttackTrigger disableAttackTrigger: world.Publish(disableAttackTrigger); break;
            case GSModRemoveItem removeItem: world.Publish(removeItem); break;
            case GSModChangeNPCState changeNpcState: world.Publish(changeNpcState); break;
            case GSModDisableTrigger disableTrigger: world.Publish(disableTrigger); break;
            case GSModRemoveModel removeModel: world.Publish(removeModel); break;
            case GSModSetTrigger setTrigger: world.Publish(setTrigger); break;
            case GSModSetNPCModifier setNpcModifier: world.Publish(setNpcModifier); break;
            default: throw new NotSupportedException($"Unsupported gamestate mod type: {message.Mod?.GetType()}");
        }
    }

    public void Update(float state)
    {
    }
}
