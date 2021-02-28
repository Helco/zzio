using zzre;

namespace zzmaps
{
    internal class TileSceneRenderData : ListDisposable
    {
        private readonly ITagContainer diContainer;
        private readonly TileScene scene;

        public TileSceneRenderData(ITagContainer diContainer, TileScene scene)
        {
            this.diContainer = diContainer;
            this.scene = scene;
        }
    }
}