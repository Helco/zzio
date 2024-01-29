using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DefaultEcs.System;
using zzre.rendering;
using zzre.rendering.effectparts;

namespace zzre.game.systems.effect;

public partial class MovingPlanes : AEntitySetSystem<float>
{
    private readonly DynamicMesh effectMesh;
    private readonly IDisposable addDisposable;
    private readonly IDisposable removeDisposable;

    public MovingPlanes(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
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
        entity.Set(new components.effect.MovingPlanesState(
            effectMesh.RentVertices(data.disableSecondPlane ? 4 : 8),
            effectMesh.RentIndices(data.disableSecondPlane ? 6 : 12),
            EffectPartUtility.GetTileUV(data.tileW, data.tileH, data.tileId))
        {
            CurPhase1 = data.phase1 / 1000f,
            CurPhase2 = data.phase2 / 1000f,
            CurRotation = 0f,
            CurTexShift = 0f,
            CurScale = 1f,
            PrevProgress = playback.CurProgress
        });
    }

    [Update]
    private void Update(
        float elapsedTime,
        in components.Parent parentComponent,
        ref components.effect.MovingPlanesState state,
        in zzio.effect.parts.MovingPlanes data)
    {
    }
}
