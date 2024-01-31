using System;
using System.Numerics;
using DefaultEcs.System;
using zzre.materials;
using zzre.rendering.effectparts;
using zzio;
using DefaultEcs.Resource;

namespace zzre.game.systems.effect;

public partial class MovingPlanes : AEntityMultiMapSystem<float, components.Parent>
{
    private readonly EffectMesh effectMesh;
    private readonly IDisposable addDisposable;
    private readonly IDisposable removeDisposable;

    public MovingPlanes(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        effectMesh = diContainer.GetTag<EffectMesh>();
        addDisposable = World.SubscribeEntityComponentAdded<zzio.effect.parts.MovingPlanes>(HandleAddedComponent);
        removeDisposable = World.SubscribeEntityComponentRemoved<components.effect.MovingPlanesState>(HandleRemovedComponent);
    }

    public override void Dispose()
    {
        base.Dispose();
        addDisposable.Dispose();
        removeDisposable.Dispose();
    }

    private void HandleRemovedComponent(in DefaultEcs.Entity entity, in components.effect.MovingPlanesState state)
    {
        effectMesh.ReturnVertices(state.VertexRange);
        effectMesh.ReturnIndices(state.IndexRange);
    }

    private void HandleAddedComponent(in DefaultEcs.Entity entity, in zzio.effect.parts.MovingPlanes data)
    {
        var playback = entity.Get<components.Parent>().Entity.Get<components.effect.CombinerPlayback>();
        var vertexRange = effectMesh.RentVertices(data.disableSecondPlane ? 4 : 8);
        var indexRange = effectMesh.RentQuadIndices(vertexRange);
        entity.Set(new components.effect.MovingPlanesState(
            vertexRange,
            indexRange,
            EffectPartUtility.GetTileUV(data.tileW, data.tileH, data.tileId))
        {
            PrevProgress = playback.CurProgress
        });
        Reset(ref entity.Get<components.effect.MovingPlanesState>(), data);
        var billboardMode = data.circlesAround || data.useDirection
            ? EffectMaterial.BillboardMode.None
            : EffectMaterial.BillboardMode.View;
        entity.Set(ManagedResource<EffectMaterial>.Create(new resources.EffectMaterialInfo(
            playback.DepthTest,
            billboardMode,
            data.renderMode,
            data.texName)));
        entity.Set(new components.effect.RenderIndices(indexRange));
    }

    private void Reset(ref components.effect.MovingPlanesState state, zzio.effect.parts.MovingPlanes data)
    {
        state.CurPhase1 = data.phase1 / 1000f;
        state.CurPhase2 = data.phase2 / 1000f;
        state.CurRotation = 0f;
        state.CurTexShift = 0f;
        state.CurScale = 1f;
    }

    [Update]
    private void Update(
        float elapsedTime,
        in components.Parent parent,
        ref components.effect.MovingPlanesState state,
        in zzio.effect.parts.MovingPlanes data,
        ref components.effect.RenderIndices indices)
    {
        ref readonly var playback = ref parent.Entity.Get<components.effect.CombinerPlayback>();
        float progressDelta = playback.CurProgress - state.PrevProgress;
        state.PrevProgress = playback.CurProgress;
        if (data.minProgress > playback.CurProgress)
        {
            indices.IndexRange = default;
            return;
        }

        indices.IndexRange = state.IndexRange;
        var curColor = data.color.ToFColor();
        if (data.manualProgress)
        {
            state.CurRotation += progressDelta;
            state.CurTexShift += elapsedTime;
            float sizeDelta = (data.targetSize - data.width) / (100f - data.minProgress) * progressDelta;
            AddScale(ref state, data, sizeDelta);
        }
        else if (state.CurPhase1 > 0f)
        {
            state.CurPhase1 -= elapsedTime;
            state.CurRotation += elapsedTime;
            state.CurTexShift += elapsedTime;
            AddScale(ref state, data, elapsedTime * data.sizeModSpeed);
        }
        else if (state.CurPhase2 > 0f)
        {
            state.CurPhase2 -= elapsedTime;
            state.CurRotation += elapsedTime;
            state.CurTexShift += elapsedTime;
            curColor *= Math.Clamp(state.CurPhase2 / (data.phase2 / 1000f), 0f, 1f);
            AddScale(ref state, data, elapsedTime * data.sizeModSpeed);
        }
        else
        {
            Reset(ref state, data);
            Update(elapsedTime, parent, ref state, data, ref indices);
            return;
        }
        UpdateQuads(parent, ref state, data, curColor);
    }

    private void AddScale(
        ref components.effect.MovingPlanesState state,
        zzio.effect.parts.MovingPlanes data,
        float amount)
    {
        var shouldGrow = data.targetSize > data.width;
        var curSize = new Vector2(data.width, data.height) * state.CurScale;
        if ((shouldGrow && curSize.MaxComponent() < data.targetSize) ||
            (!shouldGrow && curSize.MinComponent() > data.targetSize))
            state.CurScale += amount;
    }

    private void UpdateQuads(
        in components.Parent parent,
        ref components.effect.MovingPlanesState state,
        zzio.effect.parts.MovingPlanes data,
        IColor curColor)
    {
        var curAngle = state.CurRotation * data.rotation * MathEx.DegToRad;
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, curAngle);
        var right = Vector3.Transform(Vector3.UnitX * data.width * Math.Max(0f, state.CurScale), rotation);
        var up = Vector3.Transform(Vector3.UnitY * data.height * Math.Max(0f, state.CurScale), rotation);
        var center = data.circlesAround
            ? Vector3.Transform(Vector3.UnitY * data.yOffset, rotation)
            : Vector3.Zero;

        var location = parent.Entity.Get<Location>();
        var applyCenter = data.circlesAround || data.useDirection;
        if (applyCenter)
        {
            right = Vector3.TransformNormal(right, location.LocalToWorld);
            up = Vector3.TransformNormal(up, location.LocalToWorld);
        }
        center = Vector3.Transform(center, location.LocalToWorld);

        var newTexCoords1 = EffectPartUtility.TexShift(state.TexCoords, 2 * state.CurTexShift, data.texShift);
        effectMesh.SetQuad(state.VertexRange, 0, applyCenter, center, right, up, curColor, newTexCoords1);
        if (!data.disableSecondPlane)
        {
            var newTexCoords2 = EffectPartUtility.TexShift(state.TexCoords, 2 * state.CurTexShift, -data.texShift);
            effectMesh.SetQuad(state.VertexRange, 4, applyCenter, center, -right, up, curColor, newTexCoords2);
        }
    }
}
