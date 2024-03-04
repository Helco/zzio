using System;
using System.Numerics;
using zzre.materials;
using zzio;
using zzio.effect.parts;

namespace zzre.game.systems.effect;

public sealed class BeamStar : BaseCombinerPart<
    zzio.effect.parts.BeamStar,
    components.effect.BeamStarState>
{
    public BeamStar(ITagContainer diContainer) : base(diContainer) { }

    protected override void HandleRemovedComponent(in DefaultEcs.Entity entity, in components.effect.BeamStarState state)
    {
        effectMesh.ReturnVertices(state.VertexRange);
        effectMesh.ReturnVertices(state.IndexRange);
    }

    protected override void HandleAddedComponent(in DefaultEcs.Entity entity, in zzio.effect.parts.BeamStar data)
    {
        var playback = entity.Get<components.Parent>().Entity.Get<components.effect.CombinerPlayback>();
        // TODO: Use additional indices instead of vertices for BeamStar
        var vertexRange = effectMesh.RentVertices(data.complexity.GetPlaneCount() * 8);
        var indexRange = effectMesh.RentQuadIndices(vertexRange);
        entity.Set(new components.effect.BeamStarState(
            vertexRange,
            indexRange));
        Reset(ref entity.Get<components.effect.BeamStarState>(), data);

        assetRegistry.LoadEffectMaterial(entity,
            data.texName,
            EffectMaterial.BillboardMode.None,
            data.renderMode,
            playback.DepthTest);
        entity.Set(new components.effect.RenderIndices(indexRange));
    }

    private static void Reset(ref components.effect.BeamStarState state, zzio.effect.parts.BeamStar data)
    {
        state.CurPhase1 = data.phase1 / 1000f;
        state.CurPhase2 = data.phase2 / 1000f;
        state.CurScale = 1f;
        state.CurShrink = 0f;
        state.CurRotation = 0f;
        state.TexVStart = 0f;
        state.TexVRange = data.startTexVEnd;
        state.StartColor = state.EndColor = data.color;
    }

    private static float NormPhase(float current, uint total) =>
        Math.Clamp(current / (total / 1000f), 0f, 1f);

    protected override void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        in components.Parent parent,
        ref components.effect.BeamStarState state,
        in zzio.effect.parts.BeamStar data,
        ref components.effect.RenderIndices indices)
    {
        if (state.CurPhase1 <= 0f && state.CurPhase2 <= 0f)
        {
            indices = default;
            return;
        }

        ref readonly var playback = ref parent.Entity.Get<components.effect.CombinerPlayback>();
        switch(data.mode)
        {
            case BeamStarMode.Constant:
                if (state.CurPhase1 > 0f)
                    state.CurPhase1 -= elapsedTime;
                else if (state.CurPhase2 > 0f)
                {
                    state.CurPhase2 -= elapsedTime;
                    state.StartColor.a = state.EndColor.a =
                        data.color.ToFColor().a * NormPhase(state.CurPhase2, data.phase2);
                }
                break;
            case BeamStarMode.Color:
                if (state.CurPhase1 > 0f)
                    state.CurPhase1 -= elapsedTime;
                else if (state.CurPhase2 > 0f)
                {
                    state.CurPhase2 -= elapsedTime;
                    state.StartColor.a = data.color.ToFColor().a * NormPhase(state.CurPhase2, data.phase2);
                    state.EndColor.a = state.StartColor.a * 2f;
                }
                break;
            case BeamStarMode.Shrink:
                if (state.CurPhase1 > 0f)
                {
                    state.CurPhase1 -= elapsedTime;
                    state.StartColor.a = data.color.ToFColor().a * NormPhase(state.CurPhase1, data.phase1);
                }
                else if (state.CurPhase2 > 0f)
                {
                    state.CurPhase2 -= elapsedTime;
                    state.CurShrink = playback.Length * (1f - NormPhase(state.CurPhase2, data.phase2));
                }
                break;
            default:
                throw new NotSupportedException($"Unsupported BeamStar mode: {data.mode}");
        }

        state.TexVStart += elapsedTime * data.texShiftVStart;
        state.TexVRange += elapsedTime * (data.endTexVEnd - data.startTexVEnd) / data.Duration;

        if (data.complexity != BeamStarComplexity.FourPlanes)
        {
            state.CurScale += elapsedTime * data.scaleSpeedXY;
            state.CurRotation += elapsedTime * data.rotationSpeed;
        }

        UpdateQuads(parent, ref state, data);
    }

    const float PrimaryWidth = 0.5f;
    const float SecondaryWidth = 0.35350001f;
    const float SecondaryHeight = 0.353553f;
    private void UpdateQuads(
        in components.Parent parent,
        ref components.effect.BeamStarState state,
        zzio.effect.parts.BeamStar data)
    {
        var length = parent.Entity.Get<components.effect.CombinerPlayback>().Length;
        var texVEnd = length / state.TexVRange + state.TexVStart;
        float hw = data.width * state.CurScale * PrimaryWidth;
        float fw = data.width * state.CurScale * SecondaryWidth;
        float fh = data.width * state.CurScale * SecondaryHeight;

        UpdateQuad(ref state, 0, length, texVEnd, 0f, hw);
        if (data.complexity is BeamStarComplexity.TwoPlanes or BeamStarComplexity.FourPlanes)
            UpdateQuad(ref state, 1, length, texVEnd, hw, 0f);
        if (data.complexity is BeamStarComplexity.FourPlanes)
        {
            UpdateQuad(ref state, 2, length, texVEnd, fw, fh);
            UpdateQuad(ref state, 3, length, texVEnd, -fw, fh);
        }

        var matrix = parent.Entity.Get<Location>().LocalToWorld;
        if (data.complexity is not BeamStarComplexity.FourPlanes)
            matrix =
                Matrix4x4.CreateFromAxisAngle(Vector3.UnitZ, state.CurRotation * MathEx.DegToRad) *
                matrix;

        var allPos = effectMesh.Pos.Write(state.VertexRange);
        foreach (ref var pos in allPos)
            pos = Vector3.Transform(pos, matrix);
    }

    private void UpdateQuad(
        ref components.effect.BeamStarState state,
        int planeI, float length, float texVEnd, float w, float h)
    {
        var pos = effectMesh.Pos.Write(state.VertexRange.Sub(planeI * 8, 8));
        var uv = effectMesh.UV.Write(state.VertexRange.Sub(planeI * 8, 8));
        var color = effectMesh.Color.Write(state.VertexRange.Sub(planeI * 8, 8));

        pos[0] = new(-w, -h, state.CurShrink);
        pos[1] = new(+w, +h, state.CurShrink);
        pos[2] = new(+w, +h, length);
        pos[3] = new(-w, -h, length);
        pos[4] = new(-w, -h, length);
        pos[5] = new(+w, +h, length);
        pos[6] = new(+w, +h, state.CurShrink);
        pos[7] = new(-w, -h, state.CurShrink);

        uv[0] = new(0f, state.TexVStart);
        uv[1] = new(1f, state.TexVStart);
        uv[2] = new(1f, texVEnd);
        uv[3] = new(0f, texVEnd);
        uv[4] = new(0f, texVEnd);
        uv[5] = new(1f, texVEnd);
        uv[6] = new(1f, state.TexVStart);
        uv[7] = new(0f, state.TexVStart);

        color[0] = color[1] = color[6] = color[7] = state.StartColor;
        color[2..6].Fill(state.EndColor);
    }
}
