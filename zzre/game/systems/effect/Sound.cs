namespace zzre.game.systems.effect;

public sealed class Sound : BaseCombinerPart<
    zzio.effect.parts.Sound,
    components.effect.SoundState>
{
    private readonly DefaultEcs.World world;

    public Sound(ITagContainer diContainer, bool isUsedInTool = false) : base(diContainer)
    {
        var uiContainer = isUsedInTool
            ? diContainer
            : diContainer.GetTag<UI>();
        IsEnabled = uiContainer.HasTag<SoundContext>();
        world = uiContainer.GetTag<DefaultEcs.World>();
    }

    protected override void HandleRemovedComponent(in DefaultEcs.Entity entity, in components.effect.SoundState state)
    {
        if (state.Emitter.IsAlive)
            state.Emitter.Dispose();
    }

    protected override void HandleAddedComponent(in DefaultEcs.Entity entity, in zzio.effect.parts.Sound data)
    {
        var parentLocation = entity.Get<components.Parent>().Entity.Get<Location>();
        var emitter = world.CreateEntity();
        world.Publish(new messages.SpawnSample(
            $"resources/audio/sfx/duel/{data.fileName}.wav",
            RefDistance: data.minDist,
            MaxDistance: data.maxDist,
            Volume: data.volume / 100f,
            Paused: true,
            AsEntity: emitter,
            ParentLocation: parentLocation));
        entity.Set(new components.effect.SoundState(emitter));
    }

    protected override void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        in components.Parent parent,
        ref components.effect.SoundState state,
        in zzio.effect.parts.Sound data,
        ref components.effect.RenderIndices indices)
    {
        if (!state.DidStart && parent.Entity.Get<components.effect.CombinerPlayback>().CurProgress > 0f)
        {
            world.Publish(new messages.UnpauseEmitter(state.Emitter));
            state.DidStart = true;
        }
    }
}
