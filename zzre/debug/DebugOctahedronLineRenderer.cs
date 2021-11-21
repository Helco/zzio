using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre
{
    public class DebugOctahedronLineRenderer : BaseDisposable, IRenderable
    {
        private readonly DeviceBuffer vertexBuffer;
        private IColor color = IColor.White;
        private bool isDirty = false;
        private readonly Vector3[] corners = new Vector3[6];

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

        public DebugOctahedronLineRenderer(ITagContainer diContainer)
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
            var vertices = new[]
            {
                Corners[0], Corners[1],
                Corners[1], Corners[2],
                Corners[2], Corners[3],
                Corners[3], Corners[0],

                Corners[4], Corners[0],
                Corners[4], Corners[1],
                Corners[4], Corners[2],
                Corners[4], Corners[3],

                Corners[5], Corners[0],
                Corners[5], Corners[1],
                Corners[5], Corners[2],
                Corners[5], Corners[3],
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
