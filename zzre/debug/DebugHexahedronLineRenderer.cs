using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre
{
    public class DebugHexahedronLineRenderer : BaseDisposable, IRenderable
    {
        private readonly DeviceBuffer vertexBuffer;
        private IColor color = IColor.White;
        private bool isDirty = false;
        private readonly Vector3[] corners = new Vector3[8];

        public DebugLinesMaterial Material { get; }

        public Vector3[] Corners
        {
            get
            {
                isDirty = true;
                return corners;
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

        public DebugHexahedronLineRenderer(ITagContainer diContainer)
        {
            Material = new DebugLinesMaterial(diContainer);
            var device = diContainer.GetTag<GraphicsDevice>();
            vertexBuffer = device.ResourceFactory.CreateBuffer(new BufferDescription(
                12 * 2 * ColoredVertex.Stride, BufferUsage.VertexBuffer));
            vertexBuffer.Name = $"DebugHexahedron Vertices {GetHashCode()}";
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
            var vertices = new[]
            {
                Corners[0], Corners[1],
                Corners[0], Corners[2],
                Corners[3], Corners[1],
                Corners[3], Corners[2],

                Corners[4], Corners[5],
                Corners[4], Corners[6],
                Corners[7], Corners[5],
                Corners[7], Corners[6],

                Corners[0], Corners[4],
                Corners[1], Corners[5],
                Corners[2], Corners[6],
                Corners[3], Corners[7],
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
