using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Veldrid;
using zzre;
using zzre.rendering;

namespace zzmaps
{
    class MapTileRenderer : ListDisposable
    {
        private readonly ITagContainer diContainer;
        private readonly ITagContainer localDiContainer;
        private readonly Options options;
        private readonly GraphicsDevice graphicsDevice;
        private readonly LocationBuffer locationBuffer;
        private readonly OrthoCamera camera;
        private readonly CommandList commandList;
        private readonly Framebuffer framebuffer;
        private readonly Texture depthTexture, colorTexture, stagingTexture;
        private readonly DeviceBuffer counterBuffer, counterStagingBuffer;
        private RgbaFloat backgroundColor;
        private MapTiler mapTiler = new MapTiler();
        private TileScene? scene = null;
        private TileSceneRenderData? sceneRenderData = null;

        public Fence Fence { get; }
        public TileScene? Scene
        {
            get => scene;
            set
            {
                sceneRenderData?.Dispose();
                scene?.Dispose();
                scene = value;
                if (scene != null)
                {
                    sceneRenderData = new TileSceneRenderData(localDiContainer, scene, counterBuffer);
                    mapTiler.WorldUnitBounds = scene.WorldBuffers.Sections.First().Bounds;
                    backgroundColor = options.Background switch
                    {
                        ZZMapsBackground.Clear => RgbaFloat.Clear,
                        ZZMapsBackground.Black => RgbaFloat.Black,
                        ZZMapsBackground.White => RgbaFloat.White,
                        ZZMapsBackground.Scene => scene.Scene.misc.clearColor.ToFColor().ToVeldrid(),
                        ZZMapsBackground.Fog => scene.Scene.misc.fogColor.ToFColor().ToVeldrid(),
                        _ => RgbaFloat.Clear
                    };
                }
            }
        }

        public MapTileRenderer(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            graphicsDevice = diContainer.GetTag<GraphicsDevice>();
            var resourceFactory = diContainer.GetTag<ResourceFactory>();
            options = diContainer.GetTag<Options>();
            locationBuffer = new LocationBuffer(graphicsDevice, 512);
            camera = new OrthoCamera(diContainer.ExtendedWith(locationBuffer));
            commandList = resourceFactory.CreateCommandList();
            Fence = resourceFactory.CreateFence(true);
            localDiContainer = diContainer.ExtendedWith(locationBuffer, camera);

            stagingTexture = resourceFactory.CreateTexture(GetTextureDescription(TextureUsage.Staging, PixelFormat.R8_G8_B8_A8_UNorm));
            colorTexture = resourceFactory.CreateTexture(GetTextureDescription(TextureUsage.RenderTarget, PixelFormat.R8_G8_B8_A8_UNorm));
            depthTexture = resourceFactory.CreateTexture(GetTextureDescription(TextureUsage.DepthStencil, PixelFormat.D24_UNorm_S8_UInt));
            framebuffer = resourceFactory.CreateFramebuffer(new FramebufferDescription()
            {
                DepthTarget = new FramebufferAttachmentDescription(depthTexture, 0),
                ColorTargets = new[] { new FramebufferAttachmentDescription(colorTexture, 0) }
            });
            counterBuffer = resourceFactory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = sizeof(uint),
                StructureByteStride = sizeof(uint),
                Usage = BufferUsage.StructuredBufferReadWrite,
                RawBuffer = false
            });
            counterStagingBuffer = resourceFactory.CreateBuffer(new BufferDescription(sizeof(uint), BufferUsage.Staging));

            AddDisposable(commandList);
            AddDisposable(Fence);
            AddDisposable(camera);
            AddDisposable(locationBuffer);
            AddDisposable(colorTexture);
            AddDisposable(depthTexture);
            AddDisposable(stagingTexture);
            AddDisposable(framebuffer);
            AddDisposable(counterBuffer);
            AddDisposable(counterStagingBuffer);
        }

        private TextureDescription GetTextureDescription(TextureUsage usage, PixelFormat format)
        {
            return new TextureDescription()
            {
                Type = TextureType.Texture2D,
                Usage = usage,
                Format = format,
                Width = options.TileSize,
                Height = options.TileSize,
                Depth = 1,
                ArrayLayers = 1,
                MipLevels = 1,
                SampleCount = TextureSampleCount.Count1
            };
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            sceneRenderData?.Dispose();
            scene?.Dispose();
        }
        
        public (Texture texture, uint pixelCounter) RenderTile(TileID tile)
        {
            if (sceneRenderData == null)
                throw new InvalidOperationException("No scene was set");
            var tileUnitBounds = mapTiler.TileUnitBoundsFor(tile);
            if (!tileUnitBounds.Intersects(mapTiler.WorldUnitBounds))
                return (null!, 0);

            uint pixelCounter = 0;
            Fence.Reset();
            commandList.Begin();
            commandList.UpdateBuffer(counterBuffer, 0, pixelCounter);
            commandList.SetFramebuffer(framebuffer);
            commandList.ClearColorTarget(0, backgroundColor);
            commandList.ClearDepthStencil(1f);
            camera.Bounds = tileUnitBounds;
            camera.Update(commandList);
            locationBuffer.Update(commandList);
            sceneRenderData.Render(commandList, camera.Bounds);
            commandList.CopyBuffer(counterBuffer, 0, counterStagingBuffer, 0, sizeof(uint));
            commandList.CopyTexture(colorTexture, stagingTexture);
            commandList.End();
            graphicsDevice.SubmitCommands(commandList, Fence);
            graphicsDevice.WaitForFence(Fence);

            var counterMap = graphicsDevice.Map<uint>(counterStagingBuffer, MapMode.Read);
            pixelCounter = counterMap[0];
            graphicsDevice.Unmap(counterStagingBuffer);
            return (stagingTexture, pixelCounter);
        }

        public IEnumerable<(Texture, TileID, uint)> RenderTiles()
        {
            if (sceneRenderData == null)
                throw new InvalidOperationException("No scene was set");
            foreach (var tile in mapTiler.Tiles)
            {
                var (texture, pixelCounter) = RenderTile(tile);
                yield return (texture, tile, pixelCounter);
            }
        }
    }
}
