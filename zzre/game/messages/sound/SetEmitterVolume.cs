namespace zzre.game.messages;

public readonly record struct SetEmitterVolume(DefaultEcs.Entity Emitter, float Volume);
