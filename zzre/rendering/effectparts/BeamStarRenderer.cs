using System;
using System.Numerics;
using Veldrid;
using zzio.effect;
using zzio.effect.parts;
using zzre.materials;

namespace zzre.rendering.effectparts
{
    public class BeamStarRenderer : ListDisposable, IEffectPartBeamRenderer
    {
        private readonly IQuadMeshBuffer<EffectVertex> quadMeshBuffer;
        private readonly EffectMaterial material;
        private readonly BeamStar data;
        private readonly Range quadRange;

        public IEffectPart Part => data;
        public float Length { get; set; }

        private float CurPhase1Norm => Math.Clamp(curPhase1 / (data.phase1 / 1000f), 0f, 1f);
        private float CurPhase2Norm => Math.Clamp(curPhase2 / (data.phase2 / 1000f), 0f, 1f);
        private Vector4 MainColor => data.color.ToFColor().ToNumerics();
        private float TexShiftVEndSpeed => (data.endTexVEnd - data.startTexVEnd) / (data.phase1 + data.phase2) * 1000f;
        private float TexVEnd => Length / texShiftVEnd + texVStart;

        private Vector4 startColor, endColor;
        private float curPhase1, curPhase2,
            texShiftVEnd, texVStart,
            curScale, curShrink, curRotation;
        private bool areQuadsDirty = true;

        public BeamStarRenderer(ITagContainer diContainer, DeviceBufferRange locationRange, BeamStar data)
        {
            this.data = data;
            var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            var camera = diContainer.GetTag<Camera>();
            quadMeshBuffer = diContainer.GetTag<IQuadMeshBuffer<EffectVertex>>();
            material = EffectMaterial.CreateFor(data.renderMode, diContainer);
            material.LinkTransformsTo(camera);
            material.World.BufferRange = locationRange;
            material.Uniforms.Value = EffectMaterialUniforms.Default;
            material.Uniforms.Ref.isBillboard = false;
            material.MainTexture.Texture = textureLoader.LoadTexture(
                IEffectPartRenderer.TexturePath, data.texName);
            material.Sampler.Value = IEffectPartRenderer.SamplerDescription;
            AddDisposable(material);

            quadRange = quadMeshBuffer.Reserve(data.complexity.GetPlaneCount() * 2);

            Reset();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            quadMeshBuffer.Release(quadRange);
        }

        public void Reset()
        {
            curPhase1 = data.phase1 / 1000f;
            curPhase2 = data.phase2 / 1000f;
            curScale = 1f;
            curShrink = 0f;
            curRotation = 0f;
            texVStart = 0f;
            texShiftVEnd = data.startTexVEnd;
            startColor = endColor = MainColor;
            areQuadsDirty = true;
        }

        public void AddTime(float deltaTime, float _)
        {
            if (curPhase1 <= 0f && curPhase2 <= 0f)
                return;

            if (data.mode == BeamStarMode.Constant)
            {
                if (curPhase1 > 0f)
                    curPhase1 -= deltaTime;
                else if (curPhase2 > 0f)
                {
                    curPhase2 -= deltaTime;
                    startColor.W = endColor.W = MainColor.W * CurPhase2Norm;
                }
            }
            else if (data.mode == BeamStarMode.Color)
            {
                if (curPhase1 > 0f)
                    curPhase1 -= deltaTime;
                else if (curPhase2 > 0f)
                {
                    curPhase2 -= deltaTime;
                    startColor.W = MainColor.W * CurPhase2Norm;
                    endColor.W = MainColor.W * CurPhase2Norm * 2f;
                }
            }
            else if (data.mode == BeamStarMode.Shrink)
            {
                if (curPhase1 > 0f)
                {
                    curPhase1 -= deltaTime;
                    startColor.W = MainColor.W * CurPhase1Norm;
                }
                else
                {
                    curPhase2 -= deltaTime;
                    curShrink = Length * (1f - CurPhase2Norm);
                }
            }
            else
                throw new NotSupportedException($"Unsupported BeamStar mode {data.mode}");

            texVStart += deltaTime * data.texShiftVStart;
            texShiftVEnd += deltaTime * TexShiftVEndSpeed;

            if (data.complexity != BeamStarComplexity.FourPlanes)
            {
                curScale += deltaTime * data.scaleSpeedXY;
                curRotation += deltaTime * data.rotationSpeed;
            }

            areQuadsDirty = true;
        }

        public void Render(CommandList cl)
        {
            if (areQuadsDirty)
            {
                float hw = data.width * curScale * 0.5f;
                float fX = data.width * curScale * 0.35350001f; // original magic values
                float fY = data.width * curScale * 0.353553f;

                var vertices = quadMeshBuffer[quadRange];
                SetPlane(vertices, 0, 0f, hw);
                if (data.complexity != BeamStarComplexity.OnePlane)
                    SetPlane(vertices, 1, hw, 0f);
                if (data.complexity == BeamStarComplexity.FourPlanes)
                {
                    SetPlane(vertices, 2, fX, fY);
                    SetPlane(vertices, 3, -fX, fY);
                }

                var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, curRotation * MathF.PI / 180f);
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i].pos = Vector3.Transform(vertices[i].pos, rotation);
            }

            (material as IMaterial).Apply(cl);
            quadMeshBuffer.Render(cl, quadRange);
        }

        private void SetPlane(Span<EffectVertex> vertices, int planeI, float w, float h)
        {
            vertices = vertices.Slice(planeI * 4 * 2);

            vertices[0] = new() { pos = new(-w, -h, curShrink), color = startColor, tex = new(0f, texVStart) };
            vertices[1] = new() { pos = new(+w, +h, curShrink), color = startColor, tex = new(1f, texVStart) };
            vertices[2] = new() { pos = new(+w, +h, Length), color = endColor, tex = new(1f, TexVEnd) };
            vertices[3] = new() { pos = new(-w, -h, Length), color = endColor, tex = new(0f, TexVEnd) };

            vertices[4] = new() { pos = new(-w, -h, Length), color = endColor, tex = new(0f, TexVEnd) };
            vertices[5] = new() { pos = new(+w, +h, Length), color = endColor, tex = new(1f, TexVEnd) };
            vertices[6] = new() { pos = new(+w, +h, curShrink), color = startColor, tex = new(1f, texVStart) };
            vertices[7] = new() { pos = new(-w, -h, curShrink), color = startColor, tex = new(0f, texVStart) };
        }
    }
}
