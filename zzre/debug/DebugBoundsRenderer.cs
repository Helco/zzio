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
    public class DebugBoundsRenderer : BaseDisposable
    {
        private readonly DeviceBuffer vertexBuffer;
        private Bounds bounds;
        private IColor color = IColor.White;
        private bool isDirty = false;

        public DebugLinesMaterial Material { get; }

        public Bounds Bounds
        {
            get => bounds;
            set
            {
                bounds = value;
                isDirty = true;
            }
        }

        public IColor Color
        {
            get => color;
            set
            {
                color = value;
                isDirty = true;
            }
        }

        public DebugBoundsRenderer(ITagContainer diContainer)
        {
            Material = new DebugLinesMaterial(diContainer);
            var device = diContainer.GetTag<GraphicsDevice>();
            vertexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                12 * 2 * ColoredVertex.Stride, BufferUsage.VertexBuffer));
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            Material.Dispose();
            vertexBuffer.Dispose();
        }

        private void Regenerate(CommandList cl)
        {
            isDirty = false;
            var min = bounds.Min;
            var right = Vector3.UnitX * bounds.Size;
            var up = Vector3.UnitY * bounds.Size;
            var forward = Vector3.UnitZ * bounds.Size;
            var corners = new[]
            {
                min,
                min + right,
                min + up,
                min + right + up,
                min + forward,
                min + right + forward,
                min + up + forward,
                min + right + up + forward,
            };
            var vertices = new[]
            {
                corners[0], corners[1],
                corners[0], corners[2],
                corners[3], corners[1],
                corners[3], corners[2],

                corners[4], corners[5],
                corners[4], corners[6],
                corners[7], corners[5],
                corners[7], corners[6],

                corners[0], corners[4],
                corners[1], corners[5],
                corners[2], corners[6],
                corners[3], corners[7],
            }.Select(pos => new ColoredVertex(pos, Color)).ToArray();
            cl.UpdateBuffer(vertexBuffer, 0, vertices);
        }

        public void Render(CommandList cl)
        {
            if (isDirty)
                Regenerate(cl);
            (Material as IMaterial).Apply(cl);
            cl.SetVertexBuffer(0, vertexBuffer);
            cl.Draw(12 * 2);
        }
    }
}
