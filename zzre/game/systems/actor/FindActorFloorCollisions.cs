using System;
using System.Numerics;
using System.Linq;
using DefaultEcs.System;
using zzre.rendering;
using zzio.rwbs;

namespace zzre.game.systems;

public partial class FindActorFloorCollisions : AEntitySetSystem<float>
{
    private readonly IDisposable sceneLoadedSubscription;
    private WorldCollider? worldCollider;
    private WorldMesh? worldMesh;

    public FindActorFloorCollisions(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
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
        var coll = worldCollider
            .Intersections(line)
            .OrderByDescending(i => i.Point.Y) // equivalent to distance order
            .Select(intersection =>
            {
                if (intersection.TriangleId == null)
                    return default;
                var info = worldCollider.GetTriangleInfo(intersection.TriangleId.Value);
                var rwMaterial = worldMesh.Materials[(int)info.Atomic.matIdBase + info.VertexTriangle.m];
                var rwTexture = rwMaterial.FindChildById(SectionId.Texture, recursive: false) as RWTexture;
                var rwTextureName = rwTexture?.FindChildById(SectionId.String, recursive: false) as RWString;
                if (rwTextureName?.value is null || rwTextureName.value.StartsWith('_'))
                    return default;

                var barycentric = intersection.Triangle.Barycentric(intersection.Point);
                var colorA = info.Atomic.colors[info.VertexTriangle.v1].ToFColor();
                var colorB = info.Atomic.colors[info.VertexTriangle.v2].ToFColor();
                var colorC = info.Atomic.colors[info.VertexTriangle.v3].ToFColor();
                var color = colorA * barycentric.X + colorB * barycentric.Y + colorC * barycentric.Z;

                return new components.ActorFloorCollision(intersection.Point, color, rwTextureName.value);
            }).FirstOrDefault(c => c.TextureName is not null);

        if (coll.TextureName is null)
            entity.Remove<components.ActorFloorCollision>();
        else
            entity.Set(coll);
    }
}
