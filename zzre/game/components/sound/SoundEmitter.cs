namespace zzre.game.components;

public struct SoundEmitter(
    uint sourceId,
    float startVolume,
    float referenceDistance,
    float maxDistance,
    bool isMusic = false)
{
    public readonly uint SourceId = sourceId;
    public readonly float ReferenceDistance = referenceDistance;
    public readonly float MaxDistance = maxDistance;
    public readonly bool IsMusic = isMusic;
    public float Volume = startVolume;
}
