using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio.primitives;
using zzre.materials;
using zzre.rendering;

namespace zzre
{
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
}
