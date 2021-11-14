using System.Numerics;

namespace zzre.game.components
{
    /// <summary>
    /// An entity that controls its body location through non-parental ways (e.g. PlayerPuppet)
    /// </summary>
    public struct PuppetActorMovement
    {
        public Vector3 TargetDirection;
    }
}
