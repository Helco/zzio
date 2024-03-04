using System.Numerics;

namespace zzre.game.messages;

public readonly record struct SpawnSample(
    string SamplePath,
    DefaultEcs.Entity? AsEntity,
    float RefDistance = 10f,
    float MaxDistance = 20f,
    float Volume = 1f,
    bool Looping = false,
    bool Paused = false,
    Vector3? Position = null,
    Location? ParentLocation = null,
    AssetLoadPriority Priority = AssetLoadPriority.Synchronous)
{
    // a simple heuristic: If the caller cares about the entity, it probably needs the entity right away

    public SpawnSample(
        string SamplePath,
        float RefDistance = 10f,
        float MaxDistance = 20f,
        float Volume = 1f,
        bool Looping = false,
        bool Paused = false,
        Vector3? Position = null,
        Location? ParentLocation = null,
        AssetLoadPriority Priority = AssetLoadPriority.High) // otherwise we can take a bit more time to load the sound
        : this(SamplePath, AsEntity: null,
              RefDistance, MaxDistance, Volume,
              Looping, Paused,
              Position, ParentLocation,
              Priority)
    { }
}
