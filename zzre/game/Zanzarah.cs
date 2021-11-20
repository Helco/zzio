using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using zzio.vfs;

namespace zzre.game
{
    public interface IZanzarahContainer
    {
        Framebuffer Framebuffer { get; }
        Vector2 MousePos { get; }
        bool IsMouseCaptured { get; set; }

        bool IsMouseDown(MouseButton mouseButton);
        bool IsKeyDown(Key key);

        event Action OnResize;
        event Action<Key> OnKeyDown;
        event Action<Key> OnKeyUp;
        event Action<MouseButton, Vector2> OnMouseDown;
        event Action<MouseButton, Vector2> OnMouseUp;
        event Action<Vector2> OnMouseMove;
    }

    public class Zanzarah : ITagContainer
    {
        private const int MaxDatabaseModule = (int)(zzio.db.ModuleType.Dialog + 1); // module filenames are one-based
        private readonly ITagContainer tagContainer;
        private readonly IZanzarahContainer zanzarahContainer;

        public Game? CurrentGame { get; private set; }

        public Zanzarah(ITagContainer diContainer, IZanzarahContainer zanzarahContainer)
        {
            tagContainer = new TagContainer().FallbackTo(diContainer);
            tagContainer.AddTag(this);
            tagContainer.AddTag(zanzarahContainer);
            tagContainer.AddTag(LoadDatabase());
            this.zanzarahContainer = zanzarahContainer;
            CurrentGame = new Game(this, "sc_0231", -1);
        }

        public void Update()
        {
            CurrentGame?.Update();
        }

        public void Render(CommandList finalCommandList)
        {
            CurrentGame?.Render(finalCommandList);
        }

        private zzio.db.MappedDB LoadDatabase()
        {
            var mappedDb = new zzio.db.MappedDB();
            var resourcePool = GetTag<IResourcePool>();
            for (int i = 1; i <= MaxDatabaseModule; i++)
            {
                using var tableStream = resourcePool.FindAndOpen($"Data/_fb0x0{i}.fbs");
                if (tableStream == null)
                    continue;
                var table = new zzio.db.Table();
                table.Read(tableStream);
                mappedDb.AddTable(table);
            }
            return mappedDb;
        }

        public void Dispose() => tagContainer.Dispose();
        public ITagContainer AddTag<TTag>(TTag tag) where TTag : class => tagContainer.AddTag(tag);
        public TTag GetTag<TTag>() where TTag : class => tagContainer.GetTag<TTag>();
        public IEnumerable<TTag> GetTags<TTag>() where TTag : class => tagContainer.GetTags<TTag>();
        public bool HasTag<TTag>() where TTag : class => tagContainer.HasTag<TTag>();
        public bool RemoveTag<TTag>() where TTag : class => tagContainer.RemoveTag<TTag>();
        public bool TryGetTag<TTag>(out TTag tag) where TTag : class => tagContainer.TryGetTag(out tag);
    }
}
