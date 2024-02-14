using System.CommandLine.Invocation;
using System.CommandLine;
using Serilog;
using Silk.NET.SDL;
using Veldrid;

namespace zzre;

partial class Program
{
#if DEBUG
    private static readonly Option<bool> OptionRenderDoc = new(
        "--renderdoc",
        () => true,
        "Whether RenderDoc is to be loaded at start.\nIf RenderDoc loading makes problems set this option to \"false\"");

    private static RenderDoc? RenderDoc;
    private static ILogger RenderDocLogger = null!;

    private static void AddGlobalRenderDocOption(RootCommand command) =>
        command.Add(OptionRenderDoc);

    private static void LoadRenderDoc(ITagContainer diContainer)
    {
        RenderDocLogger = diContainer.GetLoggerFor<RenderDoc>();
        var ctx = diContainer.GetTag<InvocationContext>();
        var shouldLoad = ctx.ParseResult.GetValueForOption(OptionRenderDoc);
        if (!shouldLoad)
            return;
        if (RenderDoc.Load(out RenderDoc))
        {
            RenderDoc.APIValidation = true;
            RenderDoc.OverlayEnabled = false;
            RenderDoc.RefAllResources = true;
            RenderDocLogger.Information("Was loaded, use the PrintScreen key to capture the next frame");
        }
        else
            RenderDocLogger.Warning("Could not load");
    }

    private static void SetupRenderDocKeys(SdlWindow window)
    {
        if (RenderDoc == null)
            return;
        window.OnKey += ev =>
        {
            if (ev.Repeat != 0 || ev.Type != (uint)EventType.Keydown || (KeyCode)ev.Keysym.Sym != KeyCode.KPrintscreen)
                return;
            if (!RenderDoc.IsTargetControlConnected())
            {
                RenderDocLogger.Information("Starting Replay UI");
                RenderDoc.LaunchReplayUI();
            }
        };
    }
#else
    private static void AddGlobalRenderDocOption(RootCommand _) { }
    private static void LoadRenderDoc(ITagContainer _) { }
    private static void SetupRenderDocKeys(SdlWindow _) { }
#endif
}
