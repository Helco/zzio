namespace zzre.game.components.effect;

public struct SoundState(DefaultEcs.Entity emitter)
{
    public readonly DefaultEcs.Entity Emitter = emitter;
    public bool DidStart;
}
