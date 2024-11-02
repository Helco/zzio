using System.Numerics;

namespace zzre.game.messages;

public readonly record struct GenerateAIPath(
    DefaultEcs.Entity ForEntity,
    uint CurrentWaypointId = uint.MaxValue,
    Vector3? CurrentPosition = null)
{
}
