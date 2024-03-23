using System;
using System.Numerics;
using zzre.materials;
using zzio;

namespace zzre.game.systems.effect;

public sealed class MovingPlanes : BaseCombinerPart<
    zzio.effect.parts.MovingPlanes,
    components.effect.MovingPlanesState>
{
    public MovingPlanes(ITagContainer diContainer) : base(diContainer) { }

    protected override void HandleRemovedComponent(in DefaultEcs.Entity entity, in components.effect.MovingPlanesState state)
    {
        effectMesh.ReturnVertices(state.VertexRange);
        effectMesh.ReturnIndices(state.IndexRange);
    }

    protected override void HandleAddedComponent(in DefaultEcs.Entity entity, in zzio.effect.parts.MovingPlanes data)
    {
        var playback = entity.Get<components.Parent>().Entity.Get<components.effect.CombinerPlayback>();
        var vertexRange = effectMesh.RentVertices(data.disableSecondPlane ? 4 : 8);
        var isBillboard = !data.circlesAround && !data.useDirection;
        var indexRange = effectMesh.RentQuadIndices(vertexRange, doubleSided: !isBillboard);
        entity.Set(new components.effect.MovingPlanesState(
            vertexRange,
            indexRange,
            EffectMesh.GetTileUV(data.tileW, data.tileH, data.tileId))
        {
            PrevProgress = playback.CurProgress
        });
        Reset(ref entity.Get<components.effect.MovingPlanesState>(), data);

        assetRegistry.LoadEffectMaterial(entity,
            data.texName,
            isBillboard ? EffectMaterial.BillboardMode.View : EffectMaterial.BillboardMode.None,
            data.renderMode,
            playback.DepthTest);
        entity.Set(new components.effect.RenderIndices(indexRange));
    }

    private static void Reset(ref components.effect.MovingPlanesState state, zzio.effect.parts.MovingPlanes data)
    {
        state.CurRotation = 0f;
        state.CurTexShift = 0f;
        ResetCycle(ref state, data);
    }

    private static void ResetCycle(ref components.effect.MovingPlanesState state, zzio.effect.parts.MovingPlanes data)
    {
        state.CurPhase1 = data.phase1 / 1000f;
        state.CurPhase2 = data.phase2 / 1000f;
        state.CurScale = 1f;
    }

    protected override void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
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
        else if (playback.IsLooping)
        {
            ResetCycle(ref state, data);
            Update(elapsedTime, entity, parent, ref state, data, ref indices);
            return;
        }
        else
            return;
        UpdateQuads(parent, ref state, data, curColor);
    }

    private static void AddScale(
        ref components.effect.MovingPlanesState state,
        zzio.effect.parts.MovingPlanes data,
        float amount)
    {
        var shouldGrow = data.targetSize > data.width;
        if ((shouldGrow && state.CurScale < data.targetSize) ||
            (!shouldGrow && state.CurScale > data.targetSize))
            state.CurScale += amount;
    }

    private void UpdateQuads(
        in components.Parent parent,
        ref components.effect.MovingPlanesState state,
        zzio.effect.parts.MovingPlanes data,
        IColor curColor)
    {
        var location = parent.Entity.Get<Location>();
        var curAngle = state.CurRotation * data.rotation * MathEx.DegToRad;

        UpdateQuad(location, ref state, data, offset: 0, curAngle, state.CurTexShift, curColor);
        if (!data.disableSecondPlane)
            UpdateQuad(location, ref state, data, offset: 4, -curAngle, -state.CurTexShift, curColor);
    }

    private void UpdateQuad(
        Location location,
        ref components.effect.MovingPlanesState state,
        zzio.effect.parts.MovingPlanes data,
        int offset,
        float angle,
        float texShift,
        IColor curColor)
    {
        var center =
            Vector3.Transform(new(data.xOffset, 0, 0), location.LocalToWorld) +
            Vector3.UnitY * data.yOffset;
        var newTexCoords = EffectMesh.TexShift(state.TexCoords, 2 * texShift, data.texShift);
        var w = data.width * Math.Max(0f, state.CurScale);
        var h = data.width * Math.Max(0f, state.CurScale);
        Vector3 right, up;
        bool applyCenter = true;

        if (data.circlesAround)
        {
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle);
            right = Vector3.Transform(Vector3.UnitX * w, rotation);
            up = Vector3.Transform(Vector3.UnitZ * -h, rotation);
        }
        else if (data.useDirection)
        {
            // what even is this rotation, only used in e5021...
            var dir = location.InnerForward;
            var rotUp = Vector3.Cross(dir, Vector3.One * 0.42340001f);
            var rotRight = Vector3.Cross(dir, rotUp);
            var rotForward = Vector3.Cross(rotUp, rotRight);
            var rotMatrix = Matrix4x4.CreateFromAxisAngle(Vector3.UnitZ, angle) * new Matrix4x4(
                rotRight.X, rotRight.Y, rotRight.Z, 0f,
                rotUp.X, rotUp.Y, rotUp.Z, 0f,
                rotForward.X, rotForward.Y, rotForward.Z, 0f,
                0f, 0f, 0f, 1f);
            right = Vector3.TransformNormal(Vector3.UnitX * w, rotMatrix);
            up = Vector3.TransformNormal(Vector3.UnitY * h, rotMatrix);
        }
        else
        {
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle);
            right = Vector3.Transform(Vector3.UnitX * w, rotation);
            up = Vector3.Transform(Vector3.UnitY * h, rotation);
            applyCenter = false;
        }

        effectMesh.SetQuad(state.VertexRange, offset, applyCenter, center, right, up, curColor, newTexCoords);
    }
}
