using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Veldrid;
using zzio.effect;
using zzio.effect.parts;
using zzio.utils;
using zzio.vfs;
using zzre.materials;

namespace zzre.rendering.effectparts
{
    public class MovingPlanesRenderer : ListDisposable, IEffectCombinerPartRenderer
    {
        private readonly Camera camera;
        private readonly LocationBuffer locationBuffer;
        private readonly IQuadMeshBuffer<EffectVertex> quadMeshBuffer;
        private readonly DeviceBufferRange locationRange;
        private readonly EffectMaterial material;
        private readonly MovingPlanes data;
        private readonly Range quadRange;
        private readonly Rect texCoords;

        public IEffectPart Part => data;
        private Vector2 CurSize => new Vector2(data.width, data.height) * curScale;
        private float CurRotationAngle => curRotation * data.rotation * MathF.PI / 180f;

        private float curRotation = 0f, curTexShift = 0f,
            curPhase1, curPhase2, prevProgress = 100f;
        private float curScale = 1f;
        private Vector4 curColor;

        public Location Location { get; } = new Location();

        public MovingPlanesRenderer(ITagContainer diContainer, MovingPlanes data)
        {
            this.data = data;
            var resourcePool = diContainer.GetTag<IResourcePool>();
            var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            camera = diContainer.GetTag<Camera>();
            quadMeshBuffer = diContainer.GetTag<IQuadMeshBuffer<EffectVertex>>();
            locationBuffer = diContainer.GetTag<LocationBuffer>();
            locationRange = locationBuffer.Add(Location);
            material = EffectMaterial.CreateFor(data.renderMode, diContainer);
            material.LinkTransformsTo(camera);
            material.World.BufferRange = locationRange;
            material.Uniforms.Value = EffectMaterialUniforms.Default;
            AddDisposable(material.MainTexture.Texture = textureLoader.LoadTexture(IEffectCombinerPartRenderer.TexturePath, data.texName));
            material.Sampler.Value = SamplerAddressMode.Clamp.AsDescription(SamplerFilter.MinLinear_MagLinear_MipLinear);
            AddDisposable(material);

            quadRange = quadMeshBuffer.Reserve(data.disableSecondPlane ? 1 : 2);
            texCoords = EffectPartUtility.GetTileUV(data.tileW, data.tileH, data.tileId);

            Reset();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            quadMeshBuffer.Release(quadRange);
            locationBuffer.Remove(locationRange);
        }

        public void Render(CommandList cl)
        {
            (material as IMaterial).Apply(cl);
            quadMeshBuffer.Render(cl, quadRange);
        }

        public void Update()
        {
            if (!data.circlesAround && !data.useDirection)
                Location.LocalRotation = Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateLookAt(
                    Location.GlobalPosition, camera.Location.GlobalPosition, Vector3.UnitY));
        }

        public void AddTime(float timeDelta, float newProgress)
        {
            var progressDelta = newProgress - prevProgress;
            prevProgress = newProgress;
            if (data.minProgress > newProgress)
                return;

            if (data.manualProgress)
            {
                float sizeDelta = (data.targetSize - data.width) / (100f - data.minProgress) * progressDelta;
                curRotation += progressDelta;
                ScalePlanes(sizeDelta);
                curTexShift += timeDelta;
            }
            else if (curPhase1 > 0f)
            {
                curPhase1 -= timeDelta;
                curRotation += timeDelta;
                ScalePlanes(timeDelta * data.sizeModSpeed);
                curTexShift += timeDelta;
            }
            else if (curPhase2 > 0f)
            {
                float fadeOut = Math.Clamp(curPhase2 / (data.phase2 / 1000f), 0f, 1f);

                curPhase2 -= timeDelta;
                curColor = data.color.ToFColor().ToNumerics() * fadeOut;
                curRotation += timeDelta;
                ScalePlanes(timeDelta * data.sizeModSpeed);
                curTexShift += timeDelta;
            }
            else
            {
                Reset();
                AddTime(timeDelta, newProgress);
            }
            UpdateQuads();
        }

        private void ScalePlanes(float amount)
        {
            bool shouldGrow = data.targetSize > data.width;
            if ((shouldGrow && CurSize.MaxComponent() >= data.targetSize) ||
                (!shouldGrow && CurSize.MinComponent() <= data.targetSize))
                return;
            curScale += amount;
        }

        public void Reset()
        {
            curPhase1 = Math.Max(0.001f, data.phase1 / 1000f);
            curPhase2 = data.phase2 / 1000f;
            curScale = 1f;
            curColor = data.color.ToFColor().ToNumerics();
            UpdateQuads();
        }

        private void UpdateQuads()
        {
            float
                sinTexShift = MathF.Sin(2 * curTexShift) * data.texShift,
                cosTexShift = MathF.Cos(2 * curTexShift) * data.texShift;
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, CurRotationAngle);

            var vertices = quadMeshBuffer[quadRange];
            UpdateQuad(vertices, Vector3.UnitX);
            if (!data.disableSecondPlane)
                UpdateQuad(vertices.Slice(4), -Vector3.UnitX);
            
            void UpdateQuad(Span<EffectVertex> vertices, Vector3 right)
            {
                var preYOffset = data.circlesAround ? data.yOffset : 0f;
                var postYOffset = data.circlesAround ? 0f : data.yOffset;
                vertices[0].pos = right * -data.width + Vector3.UnitY * (preYOffset - data.height);
                vertices[1].pos = right * -data.width + Vector3.UnitY * (preYOffset + data.height);
                vertices[2].pos = right * +data.width + Vector3.UnitY * (preYOffset + data.height);
                vertices[3].pos = right * +data.width + Vector3.UnitY * (preYOffset - data.height);
                vertices[0].tex = new Vector2(texCoords.Min.X, texCoords.Min.Y);
                vertices[1].tex = new Vector2(texCoords.Min.X, texCoords.Max.Y);
                vertices[2].tex = new Vector2(texCoords.Max.X, texCoords.Max.Y);
                vertices[3].tex = new Vector2(texCoords.Max.X, texCoords.Min.Y);
                for (int i = 0; i < 4; i++)
                {
                    vertices[i].pos = Vector3.Transform(vertices[i].pos, rotation) + Vector3.UnitY * postYOffset;
                    vertices[i].color = curColor;
                }
            }
        }
    }
}
