using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.primitives;
using zzre.materials;
using zzre.rendering;

namespace zzre
{
    public class DebugLineRenderer : BaseDisposable, IRenderable
    {
        private readonly DynamicLineMeshBuffer<ColoredVertex> meshBuffer;
        public DebugLinesMaterial Material { get; }

        public DebugLineRenderer(ITagContainer diContainer)
        {
            Material = new DebugLinesMaterial(diContainer);
            meshBuffer = new DynamicLineMeshBuffer<ColoredVertex>(diContainer.GetTag<ResourceFactory>());
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            Material.Dispose();
            meshBuffer.Dispose();
        }

        public void Clear() => meshBuffer.Release(Range.All);

        public Range Add(IColor color, Vector3 start, Vector3 end) => Add(color, new Line(start, end));
        public Range Add(IColor color, params Line[] lines) => Add(color, lines as IEnumerable<Line>);
        public Range Add(IColor color, IEnumerable<Line> lines)
        {
            var range = meshBuffer.Reserve(lines.Count());
            var vertices = meshBuffer[range];
            foreach (var (line, index) in lines.Indexed())
            {
                vertices[index * 2 + 0].pos = line.Start;
                vertices[index * 2 + 1].pos = line.End;
                vertices[index * 2 + 0].color = vertices[index * 2 + 1].color = color;
            }
            return range;
        }

        public void Remove(Range range) => meshBuffer.Release(range);

        public void Render(CommandList cl)
        {
            if (meshBuffer.PrimitiveCount == 0)
                return;
            (Material as IMaterial).Apply(cl);
            meshBuffer.Render(cl);
        }
    }
}
