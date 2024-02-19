using System.Numerics;

namespace zzre.game.messages;

public readonly record struct SpawnSample(
    string SamplePath,
    float RefDistance = 10f,
    float MaxDistance = 20f,
    float Volume = 1f,
    bool Looping = false,
    bool Paused = false,
    DefaultEcs.Entity? AsEntity = null,
    Vector3? Position = null,
    Location? ParentLocation = null);
