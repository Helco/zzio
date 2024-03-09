using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.CommandLine;
using System.CommandLine.Invocation;
using Veldrid;
using zzio.vfs;
using zzre.rendering;
using zzre.tools;
using Silk.NET.SDL;
using Serilog;

namespace zzre;

internal static partial class Program
{
    private static readonly Option<string[]> OptionPools = new(
        new[] { "--pool", "-p" },
        () =>
        [
            // as if run from a directory like zanzarah/zzre or zanzarah/system
            Path.Combine(Environment.CurrentDirectory, "..", "Resources", "DATA_0.PAK"),
            Path.Combine(Environment.CurrentDirectory, "..")
        ],
        "Adds a resource pool to use (later pools overwrite previous ones).\nCurrently directories and PAK archives are supported");

    private static readonly Option<bool> OptionDebugLayers = new(
        new[] { "--debug-layers" },
#if DEBUG
        () => true,
#else
        () => false,
#endif
        "Enable Vulkan debug layers");

    private static void Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture =
            CultureInfo.DefaultThreadCurrentUICulture =
            System.Threading.Thread.CurrentThread.CurrentCulture =
            System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

        var rootCommand = new RootCommand("zzre - Engine reimplementation and modding tools for Zanzarah");
        rootCommand.AddGlobalOption(OptionPools);
        rootCommand.AddGlobalOption(OptionDebugLayers);
        AddConfigurationOptions(rootCommand);
        AddSoundOptions(rootCommand);
        AddLoggingOptions(rootCommand);
        AddGlobalRenderDocOption(rootCommand);
        AddRemoteryOptions(rootCommand);
        AddInDevCommand(rootCommand);
        rootCommand.Invoke(args);
    }

    private static ITagContainer CommonStartupBeforeWindow(InvocationContext ctx)
    {
        SdlProvider.SetMainReady = true;
        var diContainer = new TagContainer();
        diContainer
            .AddTag(ctx)
            .AddTag(CreateLogging(diContainer))
            .AddTag(CreateRemotery(diContainer))
            .AddTag(SdlProvider.SDL.Value);
        AddOpenALDevice(diContainer);
        LoadRenderDoc(diContainer);
        return diContainer;
    }

    private static void CommonStartupAfterWindow(ITagContainer diContainer)
    {
        var window = diContainer.GetTag<SdlWindow>();
        var ctx = diContainer.GetTag<InvocationContext>();
        SetupRenderDocKeys(window);
        var graphicsDevice = CreateGraphicsDevice(window, ctx);

        diContainer
            .AddTag(graphicsDevice)
            .AddTag(graphicsDevice.ResourceFactory)
            .AddTag(new StandardTextures(diContainer))
            .AddTag(new ShaderVariantCollection(graphicsDevice,
                typeof(Program).Assembly.GetManifestResourceStream("shaders.mlss")
                ?? throw new InvalidDataException("Shader set is not compiled into zzre")))
            .AddTag(new GameTime())
            .AddTag(CreateResourcePool(diContainer))
            .AddTag(CreateConfiguration(diContainer))
            .AddTag(CreateAssetRegistry(diContainer));
    }

    private static GraphicsDevice CreateGraphicsDevice(SdlWindow window, InvocationContext ctx)
    {
        var options = new GraphicsDeviceOptions()
        {
            Debug = ctx.ParseResult.GetValueForOption(OptionDebugLayers),
            HasMainSwapchain = true,
            PreferDepthRangeZeroToOne = true,
            PreferStandardClipSpaceYDirection = true,
            SyncToVerticalBlank = true
        };
        SwapchainDescription scDesc = new SwapchainDescription(
            window.CreateSwapchainSource(),
            (uint)window.Width,
            (uint)window.Height,
            options.SwapchainDepthFormat,
            options.SyncToVerticalBlank,
            colorSrgb : false);
        return GraphicsDevice.CreateVulkan(options, scDesc);
    }

    private static IResourcePool CreateResourcePool(ITagContainer diContainer)
    {
        var ctx = diContainer.GetTag<InvocationContext>();
        var logger = diContainer.GetLoggerFor<IResourcePool>();
        var pools = ctx.ParseResult.GetValueForOption(OptionPools) ?? [];
        if (!pools.Any())
            logger.Warning("No resource pools selected");
        return pools.Length switch
        {
            0 => new InMemoryResourcePool(),
            1 => CreateSingleResourcePool(logger, pools.Single()),
            _ => new CombinedResourcePool(pools.Select(p => CreateSingleResourcePool(logger, p)).ToArray())
        };
    }

    private static IResourcePool CreateSingleResourcePool(ILogger logger, string poolName)
    {
        // just to normalize
        var path = Path.Combine(Environment.CurrentDirectory, poolName);
        var ext = Path.GetExtension(path).ToLowerInvariant() ?? "";
        switch(ext)
        {
            case ".pak":
                logger.Debug("Selected PAK resource pool {PoolName}", poolName);
                return new PAKParallelResourcePool(path);
            case "":
                logger.Debug("Selected path resource pool {PoolName}", poolName);
                return new FileResourcePool(path);
            default:
                logger.Warning("Ignored resource pool {PoolName} due to unsupported extension {Ext}", poolName, ext);
                return new InMemoryResourcePool();
        }
    }

    private static IAssetRegistry CreateAssetRegistry(ITagContainer diContainer)
    {
        var registryList = new AssetRegistryList();
        diContainer.AddTag(registryList);
        var registry = new AssetRegistry("", diContainer);
        registryList.Register("Global", registry);
        SamplerAsset.Register();
        TextureAsset.Register();
        ClumpMaterialAsset.Register();
        ClumpAsset.Register();
        ActorMaterialAsset.Register();
        ActorAsset.Register();
        AnimationAsset.Register();
        WorldMaterialAsset.Register();
        WorldAsset.Register();
        EffectMaterialAsset.Register();
        EffectCombinerAsset.Register();
        UIBitmapAsset.Register();
        UITileSheetAsset.Register();
        UIPreloadAsset.Register();
        SoundAsset.Register();
        return registry;
    }

    private static void CommonCleanup(ITagContainer diContainer)
    {
        diContainer.GetTag<ILogger>().Information("Cleanup");

        // dispose graphics device and sdl last, otherwise Vulkan or OpenAL will crash
        // we should find a better solution for disposal order
        diContainer.TryGetTag(out GraphicsDevice graphicsDevice);
        diContainer.TryGetTag(out Sdl sdl);
        diContainer.TryGetTag(out OpenALDevice openALDevice);
        diContainer.RemoveTag<GraphicsDevice>(dispose: false);
        diContainer.RemoveTag<Sdl>(dispose: false);
        diContainer.RemoveTag<OpenALDevice>(dispose: false);
        diContainer.Dispose();
        graphicsDevice?.Dispose();
        openALDevice?.Dispose();
        sdl?.Dispose();
    }
}
