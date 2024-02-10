using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.RegularExpressions;
using Veldrid;
using Veldrid.Sdl2;
using zzre.imgui;
using zzre.tools;
using zzre.rendering;
using zzio;
using zzio.vfs;

namespace zzre;

internal partial class Program
{
    private static readonly Option<bool> OptionInDevLaunchGame = new Option<bool>(
        "--launch-game",
        () => true,
        "Launches a game window upon start");

    private static readonly Option<string> OptionInDevSavegame = new(
        "--savegame",
        "The path of the savegame to load (in the mounted resource pools)\nLeave empty to use a new, empty savegame");

    private static readonly Option<int?> OptionInDevScene = new(
        "--scene",
        "Overwrite the scene ID the savegame is loaded at");

    private static readonly Option<int?> OptionInDevEntry = new(
        "--entry",
        "Overwrite the entry ID the savegame is loaded at");

    private static readonly Option<string[]> OptionInDevOpen = new(
        "--open",
        "Opens one or more resources at start with their default editors by extension");

    private static readonly Option<string> OptionInDevWindowSize = new(
        "--window-size",
        () => "1536x1152",
        "The size of the overall window");
    private static readonly Regex WindowSizeRegex = new(@"^(\d+)x(\d+)$");

    private static void AddInDevCommand(RootCommand parent)
    {
        OptionInDevWindowSize.AddValidator(optionResult => WindowSizeRegex.IsMatch(optionResult.Token?.Value ?? ""));

        var command = new Command("indev",
            "This starts an environment intended for development of zzre with a game window and access to all viewers and Dear ImGui debug windows");
        command.AddOption(OptionInDevLaunchGame);
        command.AddOption(OptionInDevSavegame);
        command.AddOption(OptionInDevScene);
        command.AddOption(OptionInDevEntry);
        command.AddOption(OptionInDevOpen);
        command.AddOption(OptionInDevWindowSize);
        command.SetHandler(HandleInDev);
        parent.AddCommand(command);
    }

    private static void HandleInDev(InvocationContext ctx)
    {
        CommonStartupBeforeWindow(ctx);

        var (windowWidth, windowHeight) = ParseWindowSize(ctx);
        var window = new Sdl2Window("Zanzarah", 100, 100, windowWidth, windowHeight,
            SDL_WindowFlags.Resizable,
            threadedProcessing: false);

        var diContainer = CommonStartupAfterWindow(window, ctx);
        var graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        var windowContainer = new WindowContainer(graphicsDevice);
        var openDocumentSet = new OpenDocumentSet(diContainer);
        diContainer
            .AddTag(windowContainer)
            .AddTag(openDocumentSet)
            .AddTag(IconFont.CreateForkAwesome(graphicsDevice));

        windowContainer.MenuBar.AddButton("Tools/Model Viewer", () => new ModelViewer(diContainer));
        windowContainer.MenuBar.AddButton("Tools/Actor Viewer", () => new ActorEditor(diContainer));
        windowContainer.MenuBar.AddButton("Tools/Effect Viewer", () => new EffectEditor(diContainer));
        windowContainer.MenuBar.AddButton("Tools/World Viewer", () => new WorldViewer(diContainer));
        windowContainer.MenuBar.AddButton("Tools/Scene Viewer", () => new SceneEditor(diContainer));

        windowContainer.MenuBar.AddButton("Launch Game", () => InDevLaunchGame(diContainer, ctx));
        windowContainer.MenuBar.AddButton("ImGui Demo", () => windowContainer.ShowImGuiDemoWindow = true);

        openDocumentSet.AddEditorType<ModelViewer>("dff");
        openDocumentSet.AddEditorType<WorldViewer>("bsp");
        openDocumentSet.AddEditorType<SceneEditor>("scn");
        openDocumentSet.AddEditorType<ActorEditor>("aed");
        openDocumentSet.AddEditorType<EffectEditor>("ed");

        window.Resized += () =>
        {
            graphicsDevice.ResizeMainWindow((uint)window.Width, (uint)window.Height);
            windowContainer.HandleResize(window.Width, window.Height);
        };

        window.KeyDown += (ev) =>
        {
            if (ev.Repeat)
                return;
            windowContainer.HandleKeyEvent(ev.Key, ev.Down);
        };

        window.KeyUp += (ev) =>
        {
            if (ev.Repeat)
                return;
            windowContainer.HandleKeyEvent(ev.Key, ev.Down);
        };

        InDevLaunchGame(diContainer, ctx);
        InDevOpenResources(diContainer, ctx);

        var time = diContainer.GetTag<GameTime>();
        while (window.Exists)
        {
            time.BeginFrame();
            if (time.HasFramerateChanged)
                window.Title = $"Zanzarah | {graphicsDevice.BackendType} | {time.FormattedStats}";

            windowContainer.Render();
            graphicsDevice.SwapBuffers();
            var inputSnapshot = window.PumpEvents();
            windowContainer.Update(time, inputSnapshot);

            time.EndFrame();
        }

        CommonCleanup(diContainer);
    }

    private static (int, int) ParseWindowSize(InvocationContext ctx)
    {
        var value = ctx.ParseResult.GetValueForOption(OptionInDevWindowSize) ?? "";
        var match = WindowSizeRegex.Match(value);
        return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
    }

    private static void InDevLaunchGame(ITagContainer diContainer, InvocationContext ctx)
    {
        var shouldLaunch = ctx.ParseResult.GetValueForOption(OptionInDevLaunchGame);
        if (!shouldLaunch)
            return;

        var savegame = new Savegame() { sceneId = 2800 };
        var savegamePath = ctx.ParseResult.GetValueForOption(OptionInDevSavegame);
        if (!string.IsNullOrWhiteSpace(savegamePath))
        {
            using var stream = diContainer.GetTag<IResourcePool>().FindAndOpen(savegamePath);
            if (stream != null)
                savegame = Savegame.ReadNew(stream);
        }
        var sceneId = ctx.ParseResult.GetValueForOption(OptionInDevScene);
        if (sceneId.HasValue)
            savegame.sceneId = sceneId.Value;
        var entryId = ctx.ParseResult.GetValueForOption(OptionInDevEntry);
        if (entryId.HasValue)
            savegame.entryId = entryId.Value;

        new ZanzarahWindow(diContainer, savegame);
    }

    private static void InDevOpenResources(ITagContainer diContainer, InvocationContext ctx)
    {
        var resourcePaths = ctx.ParseResult.GetValueForOption(OptionInDevOpen) ?? Array.Empty<string>();
        var openDocumentSet = diContainer.GetTag<OpenDocumentSet>();
        foreach (var path in resourcePaths)
            openDocumentSet.Open(path);
    }
}
