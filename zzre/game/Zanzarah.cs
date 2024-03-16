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
    ITagContainer DIContainer { get; }
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
    public DuelGame? DuelGame { get; private set; }
    public Game? CurrentGame => DuelGame as Game ?? OverworldGame;
    public UI UI { get; }

    private Zanzarah(IZanzarahContainer zanzarahContainer, Savegame? savegame = null)
    {
        var diContainer = zanzarahContainer.DIContainer;
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
    }

    public static Zanzarah StartInOverworld(IZanzarahContainer container, Savegame? savegame = null)
    {
        var zz = new Zanzarah(container, savegame);
        zz.OverworldGame = new OverworldGame(zz, savegame ?? new());
        return zz;
    }

    public static Zanzarah StartInTestDuel(IZanzarahContainer container, messages.StartDuel duel)
    {
        var zz = new Zanzarah(container, duel.Savegame);
        zz.DuelGame = new DuelGame(zz, duel);
        return zz;
    }

    internal static Zanzarah StartInTestDuel(IZanzarahContainer container, TestDuelConfig duelConfig)
    {
        var zz = new Zanzarah(container, null);
        var db = zz.GetTag<zzio.db.MappedDB>();
        zz.DuelGame = new DuelGame(zz, duelConfig.ConvertToMessage(db));
        return zz;
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
        DuelGame?.Dispose();
        DuelGame = null;
        OverworldGame?.Dispose();
        OverworldGame = null;
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
