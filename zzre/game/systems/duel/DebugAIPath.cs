using System.Numerics;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.systems;

public sealed partial class DebugAIPath : AEntitySetSystem<CommandList>
{
    private readonly DebugLineRenderer lineRenderer;
    
    public DebugAIPath(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        lineRenderer = new(diContainer);
        lineRenderer.Material.LinkTransformsTo(diContainer.GetTag<Camera>());
        lineRenderer.Material.World.Ref = Matrix4x4.Identity;
    }

    protected override void PreUpdate(CommandList cl)
    {
        base.PreUpdate(cl);
        lineRenderer.Clear();
    }

    [Update]
    private void Update(in components.AIPath aiPath)
    {
        var pathFinder = World.Get<PathFinder>();
        foreach (var index in aiPath.WaypointIds)
        {
            lineRenderer.AddDiamondSphere(new(pathFinder[index], 0.1f), IColor.White);
        }

        for (int i = 1; i < aiPath.WaypointIds.Count; i++)
        {
            lineRenderer.Add(IColor.White,
                pathFinder[aiPath.WaypointIds[i - 1]],
                pathFinder[aiPath.WaypointIds[i]]);
        }
    }

    protected override void PostUpdate(CommandList cl)
    {
        base.PostUpdate(cl);
        lineRenderer.Render(cl);
    }
}
