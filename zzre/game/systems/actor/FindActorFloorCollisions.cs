using System;
using System.Numerics;
using DefaultEcs.System;
using zzre.rendering;
using zzio.rwbs;
using Serilog;

namespace zzre.game.systems;

public partial class FindActorFloorCollisions : AEntitySetSystem<float>
{
    private readonly ILogger logger;
    private readonly IDisposable sceneLoadedSubscription;
    private WorldCollider? worldCollider;
    private WorldMesh? worldMesh;

    public FindActorFloorCollisions(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        logger = diContainer.GetLoggerFor<FindActorFloorCollisions>();
        sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
    }

    public override void Dispose()
    {
        base.Dispose();
        sceneLoadedSubscription.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded sceneLoaded)
    {
        worldCollider = World.Get<WorldCollider>();
        worldMesh = World.Get<WorldMesh>();
    }

    [Update]
    private void Update(
        in DefaultEcs.Entity entity,
        Location location,
        components.FindActorFloorCollisions config)
    {
        if (worldCollider == null || worldMesh == null)
            return;

        var line = new Line(location.GlobalPosition, location.GlobalPosition - Vector3.UnitY * config.MaxDistance);
        var intersections = new PooledList<Intersection>(16);
        worldCollider.Intersections(line, ref intersections);
        intersections.Span.Sort((a, b) => MathF.Sign(b.Point.Y - a.Point.Y)); // top to bottom is equivalent to distance
        components.ActorFloorCollision coll = default;
        foreach (ref readonly var intersection in intersections.Span)
        {
            if (intersection.TriangleId == null)
                continue;
            var info = worldCollider.GetTriangleInfo(intersection.TriangleId.Value);
            var rwMaterial = worldMesh.Materials[(int)info.Atomic.matIdBase + info.VertexTriangle.m];
            var rwTexture = rwMaterial.FindChildById(SectionId.Texture, recursive: false) as RWTexture;
            var rwTextureName = rwTexture?.FindChildById(SectionId.String, recursive: false) as RWString;
            if (rwTextureName?.value is null || rwTextureName.value.StartsWith('_'))
                continue;

            var barycentric = intersection.Triangle.Barycentric(intersection.Point);
            var colorA = info.Atomic.colors[info.VertexTriangle.v1].ToFColor();
            var colorB = info.Atomic.colors[info.VertexTriangle.v2].ToFColor();
            var colorC = info.Atomic.colors[info.VertexTriangle.v3].ToFColor();
            var color = colorA * barycentric.X + colorB * barycentric.Y + colorC * barycentric.Z;

            coll = new components.ActorFloorCollision(intersection.Point, color, rwTextureName.value);
            break;
        }

        if (coll.TextureName is null)
        {
            entity.Remove<components.ActorFloorCollision>();
            if (intersections.IsFull)
                logger.Warning("Intersection list was satiated. Make sure nothing was lost");
        }
        else
            entity.Set(coll);
        intersections.Dispose();
    }
}
