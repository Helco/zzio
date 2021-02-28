using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Veldrid;
using zzre;

namespace zzmaps
{
    class MapTileRenderer : ListDisposable
    {
        private readonly ITagContainer diContainer;
        private readonly GraphicsDevice graphicsDevice;
        private readonly CommandList commandList;
        private MapTiler mapTiler = new MapTiler();
        private TileScene? scene = null;
        private TileSceneRenderData? sceneRenderData = null;

        public Fence Fence { get; }
        public TileScene? Scene
        {
            get => scene;
            private set
            {
                sceneRenderData?.Dispose();
                scene?.Dispose();
                scene = value;
                if (scene != null)
                    sceneRenderData = new TileSceneRenderData(diContainer, scene);
            }
        }

        public MapTileRenderer(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            graphicsDevice = diContainer.GetTag<GraphicsDevice>();
            var resourceFactory = diContainer.GetTag<ResourceFactory>();
            commandList = resourceFactory.CreateCommandList();
            Fence = resourceFactory.CreateFence(true);

            AddDisposable(commandList);
            AddDisposable(Fence);
        }
    }
}
