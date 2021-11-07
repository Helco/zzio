using System.Numerics;

namespace zzre.game.components
{
    /// <summary>
    /// An actor whose movement is controlled by some other entity in non-strictly-parent ways (e.g. player entity vs player actor)
    /// </summary>
    public struct PuppetActorMovement
    {
        public Vector3 TargetDirection;
    }
}
