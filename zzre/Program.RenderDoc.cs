using System.CommandLine;
#if DEBUG
using System.CommandLine.Invocation;
using Serilog;
using Silk.NET.SDL;
using Veldrid;
#endif

namespace zzre;

partial class Program
{
#if DEBUG
    private static readonly Option<bool> OptionRenderDoc = new(
        "--renderdoc",
        () => false,
        "Whether RenderDoc is to be loaded at start.");

    private static RenderDoc? RenderDoc;
    private static ILogger RenderDocLogger = null!;

    private static void AddGlobalRenderDocOption(RootCommand command) =>
        command.AddGlobalOption(OptionRenderDoc);

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
    private static readonly Option<bool> OptionRenderDoc = new(
        "--renderdoc",
        () => false,
        "(NOT AVAILABLE IN RELEASE BUILDS) Whether RenderDoc is to be loaded at start.")
    {
        IsHidden = true
    };

    private static void AddGlobalRenderDocOption(RootCommand command) =>
        command.AddGlobalOption(OptionRenderDoc);

    private static void LoadRenderDoc(ITagContainer _) { }
    private static void SetupRenderDocKeys(SdlWindow _) { }
#endif
}
