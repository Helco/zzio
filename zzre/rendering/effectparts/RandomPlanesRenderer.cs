using System;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio.effect;
using zzio.effect.parts;
using zzre.materials;

namespace zzre.rendering.effectparts
{
    public class RandomPlanesRenderer : ListDisposable, IEffectPartRenderer
    {
        private struct RandomPlane
        {
            public float life;
            public Vector3 pos;
            public Vector2 scale, startSize;
            public float rotation, rotSpeed, scaleSpeed;
            public Vector4 startColor, color;
            public int tileI;
            public float tileProgress;

            public Vector2 Size => Vector2.Multiply(startSize, scale);
        }

        private readonly Random random = new Random();
        private readonly IQuadMeshBuffer<EffectVertex> quadMeshBuffer;
        private readonly EffectMaterial material;
        private readonly RandomPlanes data;
        private readonly Range quadRange;
        private readonly Rect[] tileTexCoords;
        private readonly RandomPlane[] planes;

        public IEffectPart Part => data;
        private float curPhase1 = -1f, curPhase2 = -1f,
            curTexShift = 0f, // yes a single curTexShift shared for all planes... (wth Funatics?)
            spawnProgress = 0f;
        private Range aliveRange;
        private bool areQuadsDirty = true;

        public RandomPlanesRenderer(ITagContainer diContainer, DeviceBufferRange locationRange, RandomPlanes data)
        {
            this.data = data;
            var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            var camera = diContainer.GetTag<Camera>();
            quadMeshBuffer = diContainer.GetTag<IQuadMeshBuffer<EffectVertex>>();
            material = EffectMaterial.CreateFor(data.renderMode, diContainer);
            material.LinkTransformsTo(camera);
            material.World.BufferRange = locationRange;
            material.Uniforms.Value = EffectMaterialUniforms.Default;
            material.Uniforms.Ref.isBillboard = !data.circlesAround;
            AddDisposable(material.MainTexture.Texture = textureLoader.LoadTexture(
                IEffectPartRenderer.TexturePath, data.texName));
            material.Sampler.Value = SamplerAddressMode.Clamp.AsDescription(SamplerFilter.MinLinear_MagLinear_MipLinear);
            AddDisposable(material);

            planes = new RandomPlane[(int)(data.planeLife * data.spawnRate * 0.001f)];
            quadRange = quadMeshBuffer.Reserve(planes.Length);
            tileTexCoords = EffectPartUtility.GetTileUV(data.tileW, data.tileH, data.tileId, data.tileCount);

            Reset();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            quadMeshBuffer.Release(quadRange);
        }

        public void Reset()
        {
            ResetTiming();
            foreach (ref var plane in planes.AsSpan())
                plane.life = 0f;
            areQuadsDirty = true;
        }
        private void ResetTiming()
        {
            curPhase1 = data.phase1 / 1000f;
            curPhase2 = data.phase2 / 1000f;
            curTexShift = 0f;
            if (curPhase1 <= 0f)
                curPhase1 = 1f;
        }

        public void AddTime(float deltaTime, float newProgress)
        {
            foreach (ref var plane in planes.AsSpan())
                UpdatePlane(ref plane, deltaTime);

            if (!data.ignorePhases && newProgress > data.minProgress)
            {
                if (curPhase1 > 0f)
                    curPhase1 -= deltaTime;
                else if (curPhase2 > 0f)
                    curPhase2 -= deltaTime;
                else
                {
                    ResetTiming();
                    AddTime(0f, newProgress);
                }
            }
            
            if (data.ignorePhases || curPhase1 > 0f || curPhase2 > 0f)
                SpawnPlanes(deltaTime);
            areQuadsDirty = true;
        }

        private void UpdatePlane(ref RandomPlane plane, float deltaTime)
        {
            if (plane.life <= 0f)
                return;

            plane.life -= deltaTime;
            plane.color = plane.startColor * Math.Clamp(plane.life / (data.planeLife / 1000f), 0f, 1f);
            plane.rotation += plane.rotSpeed * data.rotationSpeedMult * deltaTime;
            curTexShift += deltaTime;

            var scaleDelta = plane.scaleSpeed * data.scaleSpeedMult * deltaTime;
            if (data.targetSize <= data.amplPosX && plane.Size.MinComponent() > data.targetSize)
                plane.scale -= Vector2.One * scaleDelta;
            if (data.targetSize > data.amplPosX && plane.Size.MaxComponent() < data.targetSize)
                plane.scale += Vector2.One * scaleDelta;

            plane.tileProgress += deltaTime;
            if (plane.tileProgress >= data.tileDuration / 1000f)
            {
                plane.tileProgress = 0f;
                plane.tileI = (plane.tileI + 1) % (int)data.tileCount;
            }
        }

        private void SpawnPlanes(float deltaTime)
        {
            spawnProgress += data.spawnRate * deltaTime;
            int spawnCount = (int)spawnProgress;
            spawnProgress -= MathF.Truncate(spawnProgress);

            foreach (ref var plane in planes.AsSpan())
            {
                if (spawnCount == 0)
                    break;
                if (plane.life > 0f)
                    continue;
                spawnCount--;

                plane.life = data.planeLife / 1000f;
                plane.startSize = new Vector2(data.width, data.height);
                plane.scale = Vector2.One;
                plane.rotation = 0f;
                plane.scaleSpeed = random.Next(data.minScaleSpeed, data.maxScaleSpeed) * random.NextSign();
                plane.rotSpeed = random.Next(data.minScaleSpeed, data.maxScaleSpeed) * random.NextSign();
                plane.tileI = random.Next((int)data.tileCount);
                plane.tileProgress = random.Next(0f, data.tileDuration / 1000f);
                plane.startColor = plane.color = new Vector4(random.InPositiveCube() * data.amplColor / 255f, 0f) +
                    data.color.ToFColor().ToNumerics();

                var amplPos = new Vector2(data.amplPosX, data.amplPosY);
                plane.pos = new Vector3(Vector2.Multiply(random.InPositiveSquare(), amplPos) - amplPos / 2f, 0f) +
                    new Vector3(data.minPosX, data.yOffset, 0f);
            }
        }

        public void Render(CommandList cl)
        {
            if (areQuadsDirty)
            {
                areQuadsDirty = false;
                var count = 0;
                foreach (var plane in planes.Where(p => p.life > 0f))
                    UpdateQuad(in plane, quadRange.Start.Offset(count++));
                aliveRange = quadRange.Start..quadRange.Start.Offset(count);
            }

            (material as IMaterial).Apply(cl);
            quadMeshBuffer.Render(cl, aliveRange);
        }

        private void UpdateQuad(in RandomPlane plane, Index index)
        {
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, plane.rotation * MathF.PI / 180f);
            var center = data.circlesAround ? Vector3.Transform(plane.pos, rotation) : plane.pos;
            var right = Vector3.Transform(Vector3.UnitX * plane.Size.X, rotation);
            var up = Vector3.Transform(Vector3.UnitY * plane.Size.Y, rotation);
            var texCoords = tileTexCoords[plane.tileI]; // TODO: Apply texture shifting

            quadMeshBuffer[index].UpdateQuad(center, right, up, plane.color, texCoords);
        }
    }
}
