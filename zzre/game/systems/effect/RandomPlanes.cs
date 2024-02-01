using System;
using System.Numerics;
using DefaultEcs.Resource;
using zzre.materials;
using zzre.rendering.effectparts;
using zzio;
using System.Buffers;

namespace zzre.game.systems.effect;

public sealed class RandomPlanes : BaseCombinerPart<
    zzio.effect.parts.RandomPlanes,
    components.effect.RandomPlanesState>
{
    private readonly MemoryPool<components.effect.RandomPlanesState.RandomPlane> planeMemoryPool;
    private readonly Random random = Random.Shared;

    public RandomPlanes(ITagContainer diContainer) : base(diContainer)
    {
        planeMemoryPool = MemoryPool<components.effect.RandomPlanesState.RandomPlane>.Shared;
    }

    public override void Dispose()
    {
        base.Dispose();
        planeMemoryPool.Dispose();
    }

    protected override void HandleRemovedComponent(in DefaultEcs.Entity entity, in components.effect.RandomPlanesState state)
    {
        effectMesh.ReturnVertices(state.VertexRange);
        effectMesh.ReturnIndices(state.IndexRange);
        state.PlaneMemoryOwner.Dispose();
    }

    protected override void HandleAddedComponent(in DefaultEcs.Entity entity, in zzio.effect.parts.RandomPlanes data)
    {
        var playback = entity.Get<components.Parent>().Entity.Get<components.effect.CombinerPlayback>();
        int maxPlaneCount = (int)(data.planeLife * data.spawnRate / 1000f);
        var planeMemoryOwner = planeMemoryPool.Rent(maxPlaneCount);
        var vertexRange = effectMesh.RentVertices(maxPlaneCount * 4);
        var indexRange = effectMesh.RentQuadIndices(vertexRange);
        entity.Set(new components.effect.RandomPlanesState(
            planeMemoryOwner,
            maxPlaneCount,
            vertexRange,
            indexRange));
        Reset(ref entity.Get<components.effect.RandomPlanesState>(), data);

        var billboardMode = data.circlesAround
            ? EffectMaterial.BillboardMode.None
            : EffectMaterial.BillboardMode.View;
        entity.Set(ManagedResource<EffectMaterial>.Create(new resources.EffectMaterialInfo(
            playback.DepthTest,
            billboardMode,
            data.renderMode,
            data.texName)));
        entity.Set(new components.effect.RenderIndices(default));
    }

    private void Reset(ref components.effect.RandomPlanesState state, zzio.effect.parts.RandomPlanes data)
    {
        state.CurPhase1 = data.phase1 / 1000f;
        state.CurPhase2 = data.phase2 / 1000f;
        state.CurTexShift = 0f;
        if (state.CurPhase1 <= 0f)
            state.CurPhase1 = 1f;
    }

    protected override void Update(
        float elapsedTime,
        in components.Parent parent,
        ref components.effect.RandomPlanesState state,
        in zzio.effect.parts.RandomPlanes data,
        ref components.effect.RenderIndices indices)
    {
        foreach (ref var plane in state.Planes.Span)
            UpdatePlane(elapsedTime, ref state, data, ref plane);

        ref readonly var playback = ref parent.Entity.Get<components.effect.CombinerPlayback>();
        if (!data.ignorePhases && playback.CurProgress > data.minProgress)
        {
            if (state.CurPhase1 > 0f)
                state.CurPhase1 -= elapsedTime;
            else if (state.CurPhase2 > 0f)
                state.CurPhase2 -= elapsedTime;
            else
            {
                Reset(ref state, data);
                Update(elapsedTime, parent, ref state, data, ref indices);
                return;
            }
        }

        if (data.ignorePhases || state.CurPhase1 > 0f || state.CurPhase2 > 0f)
            SpawnPlanes(elapsedTime, ref state, data);
        UpdateQuads(parent, ref state, data, ref indices);
    }

    private void UpdatePlane(
        float elapsedTime,
        ref components.effect.RandomPlanesState state,
        in zzio.effect.parts.RandomPlanes data,
        ref components.effect.RandomPlanesState.RandomPlane plane)
    {
        if (plane.Life <= 0f)
            return;

        plane.Life -= elapsedTime;
        var normalizedLife = Math.Clamp(plane.Life / (data.planeLife / 1000f), 0f, 1f);
        plane.CurColor = plane.StartColor * normalizedLife;
        plane.Rotation += plane.RotationSpeed * data.rotationSpeedMult * elapsedTime;
        state.CurTexShift += elapsedTime; // one texshift for all planes, that is correct

        var scaleDelta = plane.ScaleSpeed * data.scaleSpeedMult * elapsedTime;
        var shouldGrow = data.targetSize > data.amplPosX;
        var curSize = new Vector2(data.width, data.height) * plane.Scale;
        if (shouldGrow && curSize.MaxComponent() < data.targetSize)
            plane.Scale += scaleDelta;
        if (!shouldGrow && curSize.MinComponent() > data.targetSize)
            plane.Scale -= scaleDelta;

        plane.TileProgress += elapsedTime;
        if (plane.TileProgress >= data.tileDuration / 1000f)
        {
            plane.TileProgress = 0f;
            plane.TileI = (plane.TileI + 1) % data.tileCount;
        }
    }

    private void SpawnPlanes(
        float elapsedTime,
        ref components.effect.RandomPlanesState state,
        in zzio.effect.parts.RandomPlanes data)
    {
        // yes, spawnRate is integer and planes per second
        state.SpawnProgress += data.spawnRate * elapsedTime;
        int spawnCount = (int)state.SpawnProgress;
        state.SpawnProgress -= spawnCount;

        foreach (ref var plane in state.Planes.Span)
        {
            if (spawnCount == 0)
                break;
            if (plane.Life > 0f)
                continue;
            spawnCount--;

            plane.Life = data.planeLife / 1000f;
            plane.Scale = 1f;
            plane.Rotation = 0f;
            plane.ScaleSpeed = random.Next(data.minScaleSpeed, data.maxScaleSpeed) * random.NextSign();
            plane.RotationSpeed = random.Next(data.minScaleSpeed, data.maxScaleSpeed) * random.NextSign();
            plane.TileI = random.Next(data.tileCount);
            plane.TileProgress = random.Next(0f, data.tileDuration / 1000f);
            plane.StartColor = plane.CurColor =
                new Vector4(random.InPositiveCube() * (data.amplColor / 255f), 0f) +
                data.color.ToFColor().ToNumerics();

            var amplPos = new Vector2(data.amplPosX, data.amplPosY);
            var minPos = new Vector2(data.minPosX, data.yOffset);
            plane.Pos = new Vector3(random.InSquare() * amplPos / 2 + minPos, 0f);
        }
    }

    private void UpdateQuads(
        in components.Parent parent,
        ref components.effect.RandomPlanesState state,
        zzio.effect.parts.RandomPlanes data,
        ref components.effect.RenderIndices indices)
    {
        var applyCenter = data.circlesAround;
        var planes = state.Planes.Span;
        int alivePlanes = 0;
        foreach (ref readonly var plane in planes)
        {
            if (plane.Life <= 0f)
                continue;
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, plane.Rotation * MathEx.DegToRad);
            var right = Vector3.Transform(Vector3.UnitX * plane.Scale * data.width, rotation);
            var up = Vector3.Transform(Vector3.UnitY * plane.Scale * data.height, rotation);
            var center = data.circlesAround 
                ? Vector3.Transform(plane.Pos, rotation)
                : plane.Pos;
            var color = plane.CurColor.ToFColor();
            var texCoords = EffectPartUtility.GetTileUV(data.tileW, data.tileH, data.tileId + plane.TileI);
            texCoords = EffectPartUtility.TexShift(texCoords, state.CurTexShift, data.texShift);

            var location = parent.Entity.Get<Location>();
            if (applyCenter)
            {
                right = Vector3.TransformNormal(right, location.LocalToWorld);
                up = Vector3.TransformNormal(up, location.LocalToWorld);
            }
            center = Vector3.Transform(center, location.LocalToWorld);

            effectMesh.SetQuad(state.VertexRange, alivePlanes * 4, applyCenter, center, right, up, color, texCoords);
            alivePlanes++;
        }

        indices = new(state.IndexRange.Sub(0..(alivePlanes * 6), effectMesh.IndexCapacity));
    }
}
