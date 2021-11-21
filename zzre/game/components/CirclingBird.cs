using System.Numerics;

namespace zzre.game.components
{
    public readonly struct CirclingBird
    {
        public Vector3 Center { get; init; }
        public float Speed { get; init; }

        public CirclingBird(zzio.scn.Trigger trigger)
        {
            Center = trigger.pos + trigger.dir * (trigger.ii2 * 0.01f);
            Speed = unchecked((int)trigger.ii3) * 0.001f;
        }
    }
}
