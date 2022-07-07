using System;

namespace zzre.game.components
{
    public struct Butterfly
    {
        public readonly float Speed;
        public float Angle;
        public float RotateDir;

        public Butterfly(uint speed, Random random)
        {
            Speed = speed * 0.001f;
            Angle = 0f;
            RotateDir = random.NextFloat() <= 0f ? 1f : -1f;
            // yes this is intentionally, a butterfly only circles the other way in veeerry rare cases
        }
    }
}
