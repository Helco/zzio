using System;
using System.Numerics;
using zzio.effect.parts;

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

        internal void SpawnLifeGravityColorDirVel(Random random, in ParticleEmitter data, out Vector3 dir)
        {
            life = 0f;
            maxLife = Math.Clamp(random.In(data.life), 0.1f, 15f);

            color = Vector4.Clamp(new Vector4(
                random.In(data.colorR),
                random.In(data.colorG),
                random.In(data.colorB),
                random.In(data.colorA)),
                Vector4.Zero, Vector4.One);

            colorMod = new Vector4(
                data.colorR.mod + random.InLine() * data.colorR.width,
                data.colorG.mod + random.InLine() * data.colorG.width,
                data.colorB.mod + random.InLine() * data.colorB.width,
                data.colorA.mod + random.InLine() * data.colorA.width)
                - color / maxLife;

            gravity = Vector3.Clamp(data.gravity.ToNumerics(), Vector3.One * -0.5f, Vector3.One * +0.5f) * 9.8f;
            gravityMod = 9.8f * data.gravityMod.ToNumerics() - gravity / maxLife;

            float horRot = random.InLine() * MathF.PI * 2f;
            float verRot = random.InLine() * MathF.PI * data.verticalDir;
            dir = (data.hasDirection ? Vector3.UnitZ : Vector3.Zero) + new Vector3(
                MathF.Cos(horRot) * MathF.Sin(verRot),
                MathF.Cos(verRot),
                MathF.Sin(horRot) * MathF.Sin(verRot));

            vel = dir * (data.minVel + random.InLine() * data.acc.width);
        }

        internal void SpawnScale(Random random, in ParticleEmitter data)
        {
            scale = random.In(data.scale);
            scaleMod = (data.scale.mod - scale) / maxLife;
        }
    }
}
