using System;
using System.Numerics;

namespace zzre.rendering.effectparts
{
    public struct BasicParticle
    {
        public float life, maxLife, scale, scaleMod;
        public Vector4 color, colorMod;
        public Vector3
            pos, prevPos,
            vel, acc,
            gravity, gravityMod;

        public void Update(float timeDelta)
        {
            if (life < 0f)
                return;
            life += timeDelta;
            if (life > maxLife)
                life = -1f;

            prevPos = pos;
            pos += vel * timeDelta;
            vel += acc * timeDelta + gravity * 9.8f * timeDelta;
            gravity += gravityMod * timeDelta;
            scale += scaleMod * timeDelta;
            color = Vector4.Clamp(color + colorMod * timeDelta, Vector4.Zero, Vector4.One);
        }
    }
}
