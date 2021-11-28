using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzio.scn;
using zzio.vfs;
using zzre.rendering;

namespace zzre.game
{
    public class UI : BaseDisposable, ITagContainer
    {
        private static readonly Vector2 CanonicalSize = new Vector2(1024f, 768f);

        private readonly ITagContainer tagContainer;
        private readonly IZanzarahContainer zzContainer;
        private readonly GameTime time;
        private readonly DefaultEcs.World ecsWorld;
        private readonly ISystem<float> updateSystems;
        private readonly ISystem<CommandList> renderSystems;
        private readonly GraphicsDevice graphicsDevice;
        private readonly DeviceBuffer projectionBuffer;

        public DeviceBuffer ProjectionBuffer => projectionBuffer;
        public Rect LogicalScreen { get; set; }
        public DefaultEcs.Entity CursorEntity { get; }
        public systems.ui.UIPreloader Preload { get; }

        public UI(ITagContainer diContainer)
        {
            tagContainer = new TagContainer().FallbackTo(diContainer);
            zzContainer = GetTag<IZanzarahContainer>();
            zzContainer.OnResize += HandleResize;
            time = GetTag<GameTime>();

            var resourceFactory = GetTag<ResourceFactory>();
            graphicsDevice = GetTag<GraphicsDevice>();
            projectionBuffer = resourceFactory.CreateBuffer(
                new BufferDescription(sizeof(float) * 4 * 4, BufferUsage.UniformBuffer));
            HandleResize();

            AddTag(this);
            AddTag(ecsWorld = new DefaultEcs.World());
            AddTag(new resources.UIBitmap(this));
            AddTag(new resources.UITileSheet(this));
            AddTag(Preload = new systems.ui.UIPreloader(this));

            CursorEntity = ecsWorld.CreateEntity();
            CursorEntity.Set<Rect>();
            CursorEntity.Set<components.Visibility>();

            updateSystems = new SequentialSystem<float>(
                new systems.ui.Cursor(this),
                new systems.ui.ImgButton(this),
                new systems.ui.Label(this),
                new systems.ui.CorrectRenderOrder(this),
                new systems.Reaper(this),
                new systems.ParentReaper(this));

            renderSystems = new SequentialSystem<CommandList>(
                new systems.ui.Batcher(this));

            var entity = ecsWorld.CreateEntity();
            var entity1 = entity;
            entity.Set(new components.ui.ElementId(1));
            entity.Set(new components.ui.RenderOrder(0));
            entity.Set(IColor.White);
            entity.Set(components.Visibility.Visible);
            entity.Set(new Rect(200f, 200f, 0f, 0f));
            entity.Set(Preload.Btn000);
            entity.Set(new components.ui.ImgButtonTiles(13, 14, 15, 16));
            
            entity = ecsWorld.CreateEntity();
            entity.Set(new components.ui.RenderOrder(0));
            entity.Set(IColor.White);
            entity.Set(new components.ui.ElementId(2));
            entity.SetSameAs<components.Visibility>(entity1);
            entity.Set(new Rect(400f, 200f, 0f, 0f));
            entity.Set(Preload.Fsp000);
            entity.Set(new components.ui.ImgButtonTiles(0, 1, 3, 3));
            entity.Set<components.ui.Active>();

            entity = ecsWorld.CreateEntity();
            entity.Set(new components.ui.RenderOrder(0));
            entity.Set(IColor.White);
            entity.Set<components.Visibility>();
            entity.Set(new Rect(300, 150f, 0f, 0f));
            entity.Set(Preload.Fnt000);
            entity.Set(new components.ui.Label(diContainer.GetTag<zzio.db.MappedDB>().GetText(new UID(0x18AFD201)).Text));
        }

        protected override void DisposeManaged()
        {
            updateSystems.Dispose();
            renderSystems.Dispose();
            tagContainer.Dispose();
            zzContainer.OnResize -= HandleResize;
        }

        public void Update() => updateSystems.Update(time.Delta);

        public void Render(CommandList cl) => renderSystems.Update(cl);

        private void HandleResize()
        {
            var fb = zzContainer.Framebuffer;
            var ratio = fb.Width / (float)fb.Height;
            var size = CanonicalSize;
            if (ratio < CanonicalSize.X / CanonicalSize.Y)
                size.Y = CanonicalSize.X / ratio;
            else
                size.X = CanonicalSize.Y * ratio;
            LogicalScreen = new Rect(CanonicalSize / 2f, size);
            
            var matrix = Matrix4x4.CreateOrthographicOffCenter(
                left: LogicalScreen.Min.X,
                right: LogicalScreen.Max.X,
                top: LogicalScreen.Min.Y,
                bottom: LogicalScreen.Max.Y,
                zNearPlane: 0.01f, // Z will always be 0.5 in the UI shader
                zFarPlane: 1f); // near/far just has to encapsulate this value
            graphicsDevice.UpdateBuffer(projectionBuffer, 0, ref matrix);
        }

        public ITagContainer AddTag<TTag>(TTag tag) where TTag : class => tagContainer.AddTag(tag);
        public TTag GetTag<TTag>() where TTag : class => tagContainer.GetTag<TTag>();
        public IEnumerable<TTag> GetTags<TTag>() where TTag : class => tagContainer.GetTags<TTag>();
        public bool HasTag<TTag>() where TTag : class => tagContainer.HasTag<TTag>();
        public bool RemoveTag<TTag>() where TTag : class => tagContainer.RemoveTag<TTag>();
        public bool TryGetTag<TTag>(out TTag tag) where TTag : class => tagContainer.TryGetTag(out tag);
    }
}
