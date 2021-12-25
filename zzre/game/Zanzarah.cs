using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using zzio.vfs;
using zzre.rendering;

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
        public UI UI { get; }

        public Zanzarah(ITagContainer diContainer, IZanzarahContainer zanzarahContainer)
        {
            tagContainer = new TagContainer().FallbackTo(diContainer);
            tagContainer.AddTag(this);
            tagContainer.AddTag<IAssetLoader<Texture>>(new TextureAssetLoader(tagContainer));
            tagContainer.AddTag(zanzarahContainer);
            tagContainer.AddTag(LoadDatabase());
            tagContainer.AddTag(UI = new UI(this));
            this.zanzarahContainer = zanzarahContainer;

            var savegame = new zzio.Savegame();
            using (var fileStream = new System.IO.FileStream(@"C:\dev\zanzarah\Save\_0004.dat", System.IO.FileMode.Open, System.IO.FileAccess.Read))
            using (var reader = new System.IO.BinaryReader(fileStream))
                savegame = zzio.Savegame.ReadNew(reader);
            savegame.sceneId = 2411;
            CurrentGame = new Game(this, savegame);

            UI.Publish<messages.ui.OpenDeck>();
        }

        public void Update()
        {
            CurrentGame?.Update();
            UI.Update();
        }

        public void Render(CommandList finalCommandList)
        {
            CurrentGame?.Render(finalCommandList);
            UI.Render(finalCommandList);
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
