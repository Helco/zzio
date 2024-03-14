using Veldrid;
using zzio;

namespace zzre.game;

public sealed class BattleGame : Game
{
    public BattleGame(ITagContainer diContainer, Savegame savegame) : base(diContainer, savegame)
    {
        AddTag(this);

        // create it now for extra priority in the scene loading events
        var worldRenderer = new systems.WorldRendererSystem(this);
        var fogModifier = new systems.FogModifier(this);

        var updateSystems = new systems.RecordingSequentialSystem<float>(this);
        this.updateSystems = updateSystems;
        updateSystems.Add(
            // Cameras
            new systems.FlyCamera(this),

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

            new systems.TriggerActivation(this),
            new systems.PlayerTriggers(this),

            // Fairies
            new systems.FairyAnimation(this),
            new systems.FairyGlowEffect(this),

            new systems.AmbientSounds(this),

            new systems.Reaper(this),
            new systems.ParentReaper(this));
        updateSystems.Add(new systems.PauseDuring(this, updateSystems.Systems));

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
    }
}
