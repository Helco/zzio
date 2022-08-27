using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using zzio;
using zzio.scn;

namespace zzre.game.systems
{
    public class Savegame : ISystem<float>
    {
        public bool IsEnabled { get; set; } = true;

        private readonly DefaultEcs.World world;
        private readonly zzio.Savegame savegame;
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
            disableAttackTriggerDisposable.Dispose();
            removeItemDisposable.Dispose();
            changeNpcStateDisposable.Dispose();
            removeModelDisposable.Dispose();
            disableTriggerDisposable.Dispose();
            setTriggerDisposable.Dispose();
            setNpcModifierDisposable.Dispose();
            gsmodForSceneDisposable.Dispose();
        }

        private void HandleDisableAttackTrigger(in GSModDisableAttackTrigger message) => HandleGSModForThisScene(message);
        private void HandleRemoveItem(in GSModRemoveItem message) => HandleGSModForThisScene(message);
        private void HandleChangeNpcState(in GSModChangeNPCState message) => HandleGSModForThisScene(message);
        private void HandleDisableTrigger(in GSModDisableTrigger message) => HandleGSModForThisScene(message);
        private void HandleRemoveModel(in GSModRemoveModel message) => HandleGSModForThisScene(message);
        private void HandleSetTrigger(in GSModSetTrigger message) => HandleGSModForThisScene(message);
        private void HandleSetNpcModifier(in GSModSetNPCModifier message) => HandleGSModForThisScene(message);

        private void HandleGSModForThisScene(IGameStateMod gsMod) => savegame.Add($"sc_{CurSceneID}", gsMod);

        private void HandleGSModForScene(in messages.GSModForScene message)
        {
            if (message.SceneId != CurSceneID)
            {
                savegame.Add($"sc_{message.SceneId}", message.Mod);
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
}
