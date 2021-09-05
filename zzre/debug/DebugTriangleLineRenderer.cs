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
    public class DebugTriangleLineRenderer : BaseDisposable, IRenderable
    {
        private readonly ResourceFactory resourceFactory;
        private DeviceBuffer? vertexBuffer;
        private IColor color = IColor.White;
        private bool isDirty = false;
        private Triangle[] triangles = new Triangle[0];

        public DebugLinesMaterial Material { get; }

        public Triangle[] Triangles
        {
            get
            {
                isDirty = triangles.Any();
                return triangles;
            }
            set
            {
                isDirty = true;
                triangles = value;
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

        public DebugTriangleLineRenderer(ITagContainer diContainer)
        {
            Material = new DebugLinesMaterial(diContainer);
            resourceFactory = diContainer.GetTag<GraphicsDevice>().ResourceFactory;
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            Material.Dispose();
            vertexBuffer?.Dispose();
        }

        private void Regenerate(CommandList cl)
        {
            isDirty = false;
            var vertices = triangles.SelectMany(t => new[]
            {
                t.A, t.B,
                t.A, t.C,
                t.B, t.C
            }).Select(pos => new ColoredVertex(pos, Color)).ToArray();
            uint sizeInBytes = (uint)vertices.Length * ColoredVertex.Stride;
            if (vertexBuffer == null || vertexBuffer.SizeInBytes < sizeInBytes)
            {
                vertexBuffer?.Dispose();
                vertexBuffer = resourceFactory.CreateBuffer(new BufferDescription(sizeInBytes, BufferUsage.VertexBuffer));
            }
            cl.UpdateBuffer(vertexBuffer, 0, vertices);
        }

        public void Render(CommandList cl)
        {
            if (triangles.Length == 0)
                return;
            if (isDirty)
                Regenerate(cl);
            (Material as IMaterial).Apply(cl);
            cl.SetVertexBuffer(0, vertexBuffer);
            cl.Draw((uint)triangles.Length * 6);
        }
    }
}
