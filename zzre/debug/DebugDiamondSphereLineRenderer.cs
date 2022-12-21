using System.Numerics;

namespace zzre;

public class DebugDiamondSphereLineRenderer : DebugOctahedronLineRenderer
{
    private Sphere bounds;

    public DebugDiamondSphereLineRenderer(ITagContainer diContainer) : base(diContainer) { }

    public Sphere Bounds
    {
        get => bounds;
        set
        {
            bounds = value;
            var right = Vector3.UnitX * bounds.Radius;
            var up = Vector3.UnitY * bounds.Radius;
            var forward = Vector3.UnitZ * bounds.Radius;
            Corners[0] = bounds.Center + right;
            Corners[1] = bounds.Center + forward;
            Corners[2] = bounds.Center - right;
            Corners[3] = bounds.Center - forward;
            Corners[4] = bounds.Center + up;
            Corners[5] = bounds.Center - up;
        }
    }
}
