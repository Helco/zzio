using System.Linq;

namespace zzre;

public class DebugBoxLineRenderer : DebugHexahedronLineRenderer
{
    private OrientedBox bounds;

    public DebugBoxLineRenderer(ITagContainer diContainer) : base(diContainer) { }

    public OrientedBox Bounds
    {
        get => bounds;
        set
        {
            bounds = value;
            bounds.Box
                .Corners(bounds.Orientation)
                .ToArray()
                .CopyTo(Corners, 0);
        }
    }
}
