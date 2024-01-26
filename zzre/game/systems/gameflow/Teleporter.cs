﻿using System;
using System.Linq;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems;

[PauseDuring(PauseTrigger.UIScreen)]
public partial class Teleporter : AEntitySetSystem<float>
{
    private enum State
    {
        Initialize,
        Leaving,
        AmyIsGone,
        FadeOff,
        Arriving,
        ThereSheIs,
        FinalWait
    }

    private const float LeavingDuration = 3.5f;
    private const float AmyIsGoneDuration = 1.5f;
    private const float ArrivingDuration = 1f;
    private const float FinalWaitDuration = 4f;

    private readonly Game game;
    private readonly UI ui;
    private readonly IDisposable triggerDisposable;
    private readonly IDisposable teleportDisposable;

    private int targetScene, targetEntry;
    private State state;
    private float timeLeft;
    private DefaultEcs.Entity fadeEntity;

    public Teleporter(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        game = diContainer.GetTag<Game>();
        ui = diContainer.GetTag<UI>();
        triggerDisposable = World.SubscribeEntityComponentAdded<components.ActiveTrigger>(HandleActiveTrigger);
        teleportDisposable = World.Subscribe<messages.Teleport>(HandleTeleport);
    }

    public override void Dispose()
    {
        base.Dispose();
        triggerDisposable.Dispose();
        teleportDisposable.Dispose();
    }

    [WithPredicate]
    private static bool IsInGameFlow(in components.GameFlow flow) => flow == components.GameFlow.Teleporter;

    private void HandleActiveTrigger(in DefaultEcs.Entity entity, in components.ActiveTrigger value)
    {
        if (!IsEnabled)
            return;

        var trigger = entity.Get<Trigger>();
        if (trigger.type != TriggerType.Elevator || trigger.ii3 == 0)
            // Teleporters (also runes) are originally called elevators with different types in ii3
            // The gameflow handling these events however are split (teleporters being called "SpecialElevator")
            return;

        game.Publish(new messages.Teleport(sceneId: unchecked((int)trigger.ii3), targetEntry: (int)trigger.ii2));
        game.Publish(new messages.CreaturePlaceToTrigger(game.PlayerEntity, (int)trigger.idx));
    }

    private void HandleTeleport(in messages.Teleport message)
    {
        if (!IsEnabled)
            return;

        game.PlayerEntity.Set(components.GameFlow.Teleporter);
        state = State.Initialize;
        targetScene = message.sceneId;
        targetEntry = message.targetEntry;
    }

    [Update]
    private new void Update(float elapsedTime, in DefaultEcs.Entity player)
    {
        timeLeft -= Math.Min(1 / 30f, elapsedTime);
        switch(state)
        {
            case State.Initialize:
                World.Publish(messages.LockPlayerControl.Forever);
                World.Publish(new messages.ResetPlayerMovement());
                player.Get<components.PlayerPuppet>().IsAnimationLocked = true;

                // GROUP: Spawn effect 4000 on teleporting
                // GROUP: Play sound effects during teleporting
                state = State.Leaving;
                timeLeft = LeavingDuration;
                break;
            
            case State.Leaving when timeLeft > 0f:
                player.Get<components.NonFairyAnimation>().Next = zzio.AnimationType.Dance;
                // GROUP: set effect progress and sound emitter volume
                break;
            case State.Leaving when timeLeft <= 0f:
                // GROUP: Fade out music/ambient
                // GROUP: toggle rootnode HUD
                World.Publish(new messages.CreatureSetVisibility(player, false));
                CreateTeleporterFlash(startDelay: 0);
                state = State.AmyIsGone;
                timeLeft = AmyIsGoneDuration;
                break;

            case State.AmyIsGone when timeLeft <= 0f:
                fadeEntity = ui.Preload.CreateStdFlashFade(parent: default);
                state = State.FadeOff;
                break;

            case State.FadeOff when IsFadedOff:
                player.Get<components.PlayerPuppet>().IsAnimationLocked = false;
                if (targetScene < 0)
                {
                    // Teleport inside same scene
                    var targetTrigger = World.GetEntities()
                        .With((in Trigger t) => t.type == TriggerType.Elevator && t.ii1 == targetEntry)
                        .AsEnumerable().First();
                    // TODO: Prevent target elevator trigger from triggering
                }
                else
                {
                    // GROUP: Fade in new music/ambient
                    game.LoadOverworldScene(targetScene, game.FindEntryTriggerForRune);
                    World.Publish(messages.LockPlayerControl.Forever); // this disables the normal timed entry lock
                }
                state = State.Arriving;
                timeLeft = ArrivingDuration;
                break;

            case State.Arriving when timeLeft <= 0f:
                fadeEntity = CreateTeleporterFlash(startDelay: 0.3f);
                state = State.ThereSheIs;
                break;

            // the original fading has a funny behaviour where it is marked as faded out 
            // for only one frame. The first check catches this frame, the second one does not.
            case State.ThereSheIs when !fadeEntity.IsAlive:
                // GROUP: Play sound effect on arriving
                // GROUP: Spawn effect 4001 on arriving
                World.Publish(new messages.CreatureSetVisibility(player, true));
                state = State.FinalWait;
                timeLeft = FinalWaitDuration;
                break;

            case State.FinalWait when timeLeft <= 0f:
                World.Publish(messages.LockPlayerControl.Unlock);
                World.Publish(new messages.LockPlayerControl(2f));
                player.Set(components.GameFlow.Normal);
                break;
        }
    }

    private bool IsFadedOff =>
        !fadeEntity.TryGet<components.ui.Fade>(out var flashFade) || flashFade.IsFadedIn;

    private DefaultEcs.Entity CreateTeleporterFlash(float startDelay) =>
        ui.Preload.CreateFullFlashFade(parent: default, zzio.IColor.White, new components.ui.Fade(
            From: 0f, To: 1f,
            startDelay,
            InDuration: 0.1f,
            SustainDelay: 0.05f,
            OutDuration: 0.1f));

    // TODO: Implement missing teleport behaviours
}
