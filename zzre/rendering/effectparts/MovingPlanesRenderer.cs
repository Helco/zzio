using System;
using System.Numerics;
using Veldrid;
using zzio.effect;
using zzio.effect.parts;
using zzre.materials;

namespace zzre.rendering.effectparts;

public class MovingPlanesRenderer : ListDisposable, IEffectPartRenderer
{
    private readonly IQuadMeshBuffer<EffectVertex> quadMeshBuffer;
    private readonly EffectMaterialLEGACY material;
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

    public MovingPlanesRenderer(ITagContainer diContainer, DeviceBufferRange locationRange, MovingPlanes data)
    {
        this.data = data;
        var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
        var camera = diContainer.GetTag<Camera>();
        quadMeshBuffer = diContainer.GetTag<IQuadMeshBuffer<EffectVertex>>();
        material = EffectMaterialLEGACY.CreateFor(data.renderMode, diContainer);
        material.LinkTransformsTo(camera);
        material.World.BufferRange = locationRange;
        material.Uniforms.Value = EffectMaterialUniforms.Default;
        material.Uniforms.Ref.isBillboard = !data.circlesAround && !data.useDirection;
        material.MainTexture.Texture = textureLoader.LoadTexture(
            IEffectPartRenderer.TexturePath, data.texName);
        material.Sampler.Value = IEffectPartRenderer.SamplerDescription;
        AddDisposable(material);

        quadRange = quadMeshBuffer.Reserve(data.disableSecondPlane ? 1 : 2);
        texCoords = EffectPartUtility.GetTileUV(data.tileW, data.tileH, data.tileId);

        Reset();
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        quadMeshBuffer.Release(quadRange);
    }

    public void Render(CommandList cl)
    {
        (material as IMaterial).Apply(cl);
        quadMeshBuffer.Render(cl, quadRange);
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
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, CurRotationAngle);
        var right = Vector3.Transform(Vector3.UnitX * data.width, rotation);
        var up = Vector3.Transform(Vector3.UnitY * data.width, rotation);
        var center = data.circlesAround
            ? Vector3.Transform(Vector3.UnitY * data.yOffset, rotation)
            : Vector3.Zero;

        var newTexCoords1 = EffectPartUtility.TexShift(texCoords, 2 * curTexShift, data.texShift);
        var newTexCoords2 = EffectPartUtility.TexShift(texCoords, 2 * curTexShift, -data.texShift);

        var vertices = quadMeshBuffer[quadRange];
        vertices.UpdateQuad(center, right, up, curColor, newTexCoords1);
        if (!data.disableSecondPlane)
            vertices[4..].UpdateQuad(center, -right, up, curColor, newTexCoords2);
    }
}
