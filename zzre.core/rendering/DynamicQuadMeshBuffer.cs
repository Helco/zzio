using Veldrid;

namespace zzre.rendering
{
    public class DynamicQuadMeshBuffer<TVertex> : DynamicPrimitiveMeshBuffer<TVertex>, IQuadMeshBuffer<TVertex> where TVertex : unmanaged
    {
        private static readonly ushort[] IndexPattern = new ushort[] { 0, 2, 1, 0, 3, 2 };

        public DynamicQuadMeshBuffer(ResourceFactory resourceFactory, int initialCapacity = 64, float growFactor = 1.5F) :
            base(resourceFactory, IndexPattern, initialCapacity, growFactor)
        {
        }
    }
}
