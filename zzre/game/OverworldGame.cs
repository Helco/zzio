using System;
using System.Linq;
using Veldrid;
using zzio;
using zzio.scn;

namespace zzre.game;

public sealed class OverworldGame : Game
{
    public OverworldGame(ITagContainer diContainer, Savegame savegame) : base(diContainer, savegame)
    {
        AddTag(this);

        // create it now for extra priority in the scene loading events
        var worldRenderer = new systems.WorldRendererSystem(this);
        var fogModifier = new systems.FogModifier(this);

        var updateSystems = new systems.RecordingSequentialSystem<float>(this);
        this.updateSystems = updateSystems;
        updateSystems.Add(
            new systems.Savegame(this),
            new systems.PlayerSpawner(this),
            new systems.PlayerControls(this),
            new systems.OpenMenuKeys(this),

            // Cameras
            new systems.FlyCamera(this),
            new systems.OverworldCamera(this),
            new systems.TriggerCamera(this),
            new systems.CreatureCamera(this),

            // Player movement
            new systems.HumanPhysics(this),
            new systems.PlayerPuppet(this),
            new systems.PuppetActorTarget(this),
            new systems.PuppetActorMovement(this),

            // Models and actors
            new systems.ModelLoader(this),
            new systems.BackdropLoader(this),
            new systems.PlantWiggle(this),
            new systems.DistanceAlphaFade(this),
            new systems.BehaviourSwing(this),
            new systems.BehaviourRotate(this),
            new systems.BehaviourUVShift(this),
            new systems.BehaviourDoor(this),
            new systems.BehaviourCityDoor(this),
            new systems.BehaviourCollectablePhysics(this),
            new systems.BehaviourCollectable(this),
            new systems.BehaviourMagicBridge(this),
            new systems.MoveToLocation(this),
            new systems.AdvanceAnimation(this),
            new systems.FindActorFloorCollisions(this),
            new systems.ActorLighting(this),

            // Effects
            fogModifier,
            new systems.effect.LensFlare(this),
            new systems.effect.EffectCombiner(this),
            new systems.effect.MovingPlanes(this),
            new systems.effect.RandomPlanes(this),
            new systems.effect.Emitter(this),
            new systems.effect.ParticleEmitter(this),
            new systems.effect.ModelEmitter(this),
            new systems.effect.BeamStar(this),
            new systems.effect.Sound(this),
            new systems.SceneSamples(this),

            // Animals
            new systems.Animal(this),
            new systems.Butterfly(this),
            new systems.CirclingBird(this),
            new systems.AnimalWaypointAI(this),
            new systems.CollectionFairy(this),

            // NPC
            new systems.NPC(this),
            new systems.NPCActivator(this),
            new systems.NPCScript(this),
            new systems.NPCMovementByState(this),
            new systems.NPCMovementByDialog(this),
            new systems.NPCIdle(this),
            new systems.NPCLookAtPlayer(this),
            new systems.NPCLookAtTrigger(this),

            new systems.TriggerActivation(this),
            new systems.PlayerTriggers(this),

            // Fairies
            new systems.OverworldFairySpawner(this),
            new systems.FairyHoverOffset(this),
            new systems.FairyHoverBehind(this),
            new systems.FairyKeepLastHover(this),
            new systems.FairyAnimation(this),
            new systems.FairyGlowEffect(this),

            // Dialogs
            new systems.DialogScript(this),
            new systems.DialogDelay(this),
            new systems.DialogFadeOut(this),
            new systems.DialogWaitForSayString(this),
            new systems.DialogTalk(this),
            new systems.DialogLookAt(this),
            new systems.DialogChoice(this),
            new systems.DialogTrading(this),
            new systems.DialogGambling(this),
            new systems.DialogChestPuzzle(this),

            new systems.NonFairyAnimation(this),
            new systems.AmbientSounds(this),

            // Gameflows
            new systems.GotCard(this),
            new systems.Doorway(this),
            new systems.UnlockDoor(this),
            new systems.Teleporter(this),

            new systems.Reaper(this),
            new systems.ParentReaper(this));
        updateSystems.Add(new systems.PauseDuring(this, updateSystems.Systems));
        ecsWorld.Publish(new messages.SetCameraMode(-1, default));

        var renderSystems = new systems.RecordingSequentialSystem<CommandList>(this);
        this.renderSystems = renderSystems;
        renderSystems.Add(
            fogModifier,
            new systems.ModelRenderer(this, components.RenderOrder.Backdrop),
            worldRenderer,
            new systems.ModelRenderer(this, components.RenderOrder.World),
            new systems.ActorRenderer(this),
            new systems.ModelRenderer(this, components.RenderOrder.EarlySolid),
            new systems.ModelRenderer(this, components.RenderOrder.EarlyAdditive),
            new systems.effect.EffectRenderer(this, components.RenderOrder.EarlyEffect),
            new systems.effect.EffectModelRenderer(this, components.RenderOrder.EarlyEffect),
            new systems.ModelRenderer(this, components.RenderOrder.Solid),
            new systems.ModelRenderer(this, components.RenderOrder.Additive),
            new systems.ModelRenderer(this, components.RenderOrder.EnvMap),
            new systems.effect.EffectRenderer(this, components.RenderOrder.Effect),
            new systems.effect.EffectModelRenderer(this, components.RenderOrder.Effect),
            new systems.ModelRenderer(this, components.RenderOrder.LateSolid),
            new systems.ModelRenderer(this, components.RenderOrder.LateAdditive),
            new systems.effect.EffectRenderer(this, components.RenderOrder.LateEffect),
            new systems.effect.EffectModelRenderer(this, components.RenderOrder.LateEffect));

        onceUpdate.Next += () => LoadScene(savegame.sceneId, () => FindEntryTrigger(savegame.entryId));
    }

    public void LoadScene(int sceneId, Func<Trigger> entryTrigger) =>
        LoadScene($"sc_{sceneId:D4}", entryTrigger);

    public void LoadScene(string sceneName, Func<Trigger> findEntryTrigger)
    {
        LoadScene(sceneName);
        ecsWorld.Publish(new messages.PlayerEntered(findEntryTrigger()));
    }

    public Trigger? TryFindTrigger(TriggerType type, int ii1 = -1)
    {
        var triggerEntity = ecsWorld
            .GetEntities()
            .With((in Trigger t) => t.type == type && (ii1 < 0 || t.ii1 == ii1))
            .AsEnumerable()
            .FirstOrDefault();
        return triggerEntity == default
            ? null
            : triggerEntity.Get<Trigger>();
    }

    public Trigger FindEntryTrigger(int targetEntry) => (targetEntry < 0
        ? (TryFindTrigger(TriggerType.SingleplayerStartpoint)
        ?? TryFindTrigger(TriggerType.SavePoint)
        ?? TryFindTrigger(TriggerType.MultiplayerStartpoint))

        : TryFindTrigger(TriggerType.Doorway, targetEntry)
        ?? TryFindTrigger(TriggerType.Elevator, targetEntry)
        ?? TryFindTrigger(TriggerType.RuneTarget, targetEntry))

        ?? throw new System.IO.InvalidDataException($"Scene does not have suitable entry trigger for {targetEntry}");

    public Trigger FindEntryTriggerForRune() =>
        TryFindTrigger(TriggerType.RuneTarget) ??
        TryFindTrigger(TriggerType.SingleplayerStartpoint) ??
        throw new System.IO.InvalidDataException($"Scene does not have suitable entry trigger for rune teleporting");
}
