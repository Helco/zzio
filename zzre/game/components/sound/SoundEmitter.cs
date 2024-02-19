namespace zzre.game.components;

public readonly record struct SoundEmitter(
    uint SourceId,
    float Volume,
    float ReferenceDistance,
    float MaxDistance);
