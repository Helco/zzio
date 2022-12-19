using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ITagContainer tagContainer;
        private readonly IZanzarahContainer zzContainer;
        private readonly GameTime time;
        private readonly DefaultEcs.World ecsWorld;
        private readonly systems.RecordingSequentialSystem<float> updateSystems;
        private readonly ISystem<CommandList> renderSystems;
        private readonly GraphicsDevice graphicsDevice;

        public DeviceBuffer ProjectionBuffer { get; }
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
            ProjectionBuffer = resourceFactory.CreateBuffer(
                // Buffer has to be multiple of 16 bytes big even though we only need 8
                new BufferDescription(sizeof(float) * 4, BufferUsage.UniformBuffer));
            ProjectionBuffer.Name = "UI Projection";
            HandleResize();

            AddTag(this);
            AddTag(ecsWorld = new DefaultEcs.World());
            AddTag(new resources.UIBitmap(this));
            AddTag(new resources.UITileSheet(this));
            AddTag(Preload = new systems.ui.UIPreloader(this));

            CursorEntity = ecsWorld.CreateEntity();
            CursorEntity.Set<Rect>();
            CursorEntity.Set<components.Visibility>();

            updateSystems = new systems.RecordingSequentialSystem<float>(this);
            updateSystems.Add(
                new systems.ui.Cursor(this),
                new systems.ui.ScrDeck(this),
                new systems.ui.ScrGotCard(this),
                new systems.ui.ButtonTiles(this),
                new systems.ui.Slider(this),
                new systems.ui.AnimatedLabel(this),
                new systems.ui.Label(this),
                new systems.ui.Tooltip(this),
                new systems.ui.CorrectRenderOrder(this),
                new systems.ui.Fade(this),
                new systems.Reaper(this),
                new systems.ParentReaper(this));

            renderSystems = new SequentialSystem<CommandList>(
                new systems.ui.Batcher(this));
        }

        protected override void DisposeManaged()
        {
            updateSystems.Dispose();
            renderSystems.Dispose();
            tagContainer.Dispose();
            zzContainer.OnResize -= HandleResize;
        }

        public void Publish<T>() => ecsWorld.Publish(default(T));
        public void Publish<T>(in T message) => ecsWorld.Publish(message);

        public void Update() => updateSystems.Update(time.Delta);

        public void Render(CommandList cl) => renderSystems.Update(cl);

        private void HandleResize()
        {
            var fb = zzContainer.Framebuffer;
            LogicalScreen = Rect.FromMinMax(
                Vector2.Zero,
                new Vector2(fb.Width, fb.Height));

            var size = LogicalScreen.Size;
            graphicsDevice.UpdateBuffer(ProjectionBuffer, 0, ref size);
        }

        public ITagContainer AddTag<TTag>(TTag tag) where TTag : class => tagContainer.AddTag(tag);
        public TTag GetTag<TTag>() where TTag : class => tagContainer.GetTag<TTag>();
        public IEnumerable<TTag> GetTags<TTag>() where TTag : class => tagContainer.GetTags<TTag>();
        public bool HasTag<TTag>() where TTag : class => tagContainer.HasTag<TTag>();
        public bool RemoveTag<TTag>() where TTag : class => tagContainer.RemoveTag<TTag>();
        public bool TryGetTag<TTag>(out TTag tag) where TTag : class => tagContainer.TryGetTag(out tag);
    }
}
