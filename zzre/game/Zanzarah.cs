using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.vfs;
using KeyCode = Silk.NET.SDL.KeyCode;

namespace zzre.game;

public interface IZanzarahContainer
{
    Framebuffer Framebuffer { get; }
    Vector2 MousePos { get; }
    bool IsMouseCaptured { get; set; }

    bool IsMouseDown(MouseButton mouseButton);
    bool IsKeyDown(KeyCode key);

    event Action OnResize;
    event Action<KeyCode> OnKeyDown;
    event Action<KeyCode> OnKeyUp;
    event Action<MouseButton, Vector2> OnMouseDown;
    event Action<MouseButton, Vector2> OnMouseUp;
    event Action<Vector2> OnMouseMove;
}

public class Zanzarah : ITagContainer
{
    private const int MaxDatabaseModule = (int)(zzio.db.ModuleType.Dialog + 1); // module filenames are one-based
    private readonly ITagContainer tagContainer;
    private readonly Remotery profiler;
    private readonly IDisposable gameConfigDisposable;

    public OverworldGame? OverworldGame { get; private set; }
    public BattleGame? BattleGame { get; private set; }
    public Game? CurrentGame => BattleGame as Game ?? OverworldGame;
    public UI UI { get; }

    public Zanzarah(ITagContainer diContainer, IZanzarahContainer zanzarahContainer, Savegame? savegame = null)
    {
        var gameConfig = new GameConfigSection();
        gameConfigDisposable = diContainer.GetConfigFor(gameConfig);
        tagContainer = new ExtendedTagContainer(diContainer);
        tagContainer
            .AddTag(this)
            .AddTag(zanzarahContainer)
            .AddTag(gameConfig)
            .AddTag(LoadDatabase())
            .AddTag(UI = new UI(this));
        profiler = diContainer.GetTag<Remotery>();

        // If savegame is null we should probably start the intro and main menu. But this is not implemented yet
        OverworldGame = new OverworldGame(this, savegame ?? new());
    }

    public void Update()
    {
        using var _ = profiler.SampleCPU("Zanzarah.Update");
        CurrentGame?.Update();
        UI.Update();
    }

    public void Render(CommandList finalCommandList)
    {
        using var _ = profiler.SampleCPU("Zanzarah.Render");
        finalCommandList.PushDebugGroup("Zanzarah");
        CurrentGame?.Render(finalCommandList);
        UI.Render(finalCommandList);
        finalCommandList.PopDebugGroup();
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

    public void Dispose()
    {
        tagContainer.Dispose();
        gameConfigDisposable.Dispose();
    }

    public ITagContainer AddTag<TTag>(TTag tag) where TTag : class => tagContainer.AddTag(tag);
    public TTag GetTag<TTag>() where TTag : class => tagContainer.GetTag<TTag>();
    public IEnumerable<TTag> GetTags<TTag>() where TTag : class => tagContainer.GetTags<TTag>();
    public bool HasTag<TTag>() where TTag : class => tagContainer.HasTag<TTag>();
    public bool RemoveTag<TTag>(bool dispose = true) where TTag : class => tagContainer.RemoveTag<TTag>(dispose);
    public bool TryGetTag<TTag>(out TTag tag) where TTag : class => tagContainer.TryGetTag(out tag);
}
