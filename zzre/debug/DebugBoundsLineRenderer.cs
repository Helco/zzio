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
    public class DebugBoundsLineRenderer : DebugHexahedronLineRenderer
    {
        private Bounds bounds;

        public DebugBoundsLineRenderer(ITagContainer diContainer) : base(diContainer) { }

        public Bounds Bounds
        {
            get => bounds;
            set
            {
                bounds = value;
                var min = bounds.Min;
                var right = Vector3.UnitX * bounds.Size;
                var up = Vector3.UnitY * bounds.Size;
                var forward = Vector3.UnitZ * bounds.Size;
                new[]
                {
                    min,
                    min + right,
                    min + up,
                    min + right + up,
                    min + forward,
                    min + right + forward,
                    min + up + forward,
                    min + right + up + forward,
                }.CopyTo(Corners, 0);
            }
        }
    }
}
