using System.CommandLine;
using System.CommandLine.Invocation;
using Serilog;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Loader;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.Enumeration;
using zzio;

namespace zzre;

internal sealed class MojoALLibraryNameContainer : SearchPathContainer
{
    public override string[] Windows64 => ["mojoal.dll"];
    public override string[] Windows86 => Windows64;
    public override string[] Linux => ["libmojoal.so"];
    public override string[] Android => Linux;
    public override string[] MacOS => ["libmojoal.dylib"];
    public override string[] IOS => ["__Internal"];
    public static readonly MojoALLibraryNameContainer Instance = new();
}

internal unsafe sealed class OpenALDevice : BaseDisposable
{
    public ILogger Logger { get; }
    public AL AL { get; }
    public ALContext ALC { get; } 
    public Device* Device { get; private set; }

    public OpenALDevice(ILogger logger, AL al, ALContext alc, Device* device)
    {
        Logger = logger;
        AL = al;
        ALC = alc;
        Device = device;
    }

    protected override void DisposeNative()
    {
        base.DisposeNative();
        if (Device is not null)
        {
            ALC.CloseDevice(Device);
            Device = null;
        }
        ALC.Dispose();
        AL.Dispose();
    }
}

unsafe partial class Program
{
    private static readonly Option<bool> OptionNoSound = new(
        "--no-sound",
        () => false,
        "Whether sound is enabled at all");

    public static void AddOpenALDevice(ITagContainer diContainer)
    {
        var logger = diContainer.GetLoggerFor<OpenALDevice>();
        var invocationContext = diContainer.GetTag<InvocationContext>();
        if (invocationContext.ParseResult.GetValueForOption(OptionNoSound))
        {
            logger.Information("Disabled by command line");
            return;
        }

        var (al, alc) = LoadOpenALLibraries();
        if (!alc.TryGetExtension(null, out Enumeration alcEnum))
        {
            logger.Error("ALC_ENUMERATION_EXT is not present");
            return;
        }

        var deviceName = alcEnum.GetString(null, GetEnumerationContextString.DefaultDeviceSpecifier);
        if (string.IsNullOrEmpty(deviceName))
        {
            logger.Error("No default device specifier was returned ({ErrorCode})", alc.GetError(null));
            return;
        }

        var device = alc.OpenDevice(deviceName);
        if (device == null)
        {
            logger.Error("Device could not be opened ({ErrorCode})", alc.GetError(null));
            return;
        }

        var openAlDevice = new OpenALDevice(logger, al, alc, device);
        diContainer.AddTag(openAlDevice);
        logger.Information("Opened {DeviceName} ({Vendor} {Version})",
            alc.GetContextProperty(device, GetContextString.DeviceSpecifier),
            al.GetStateProperty(StateString.Renderer),
            al.GetStateProperty(StateString.Version));
    }

    private static (AL, ALContext) LoadOpenALLibraries()
    {
        // We need a custom name container, so we have to jump through some other hoops as well :(

        var ctx = new MultiNativeContext(AL.CreateDefaultContext(MojoALLibraryNameContainer.Instance.GetLibraryNames()), null);
        var al = new AL(ctx);
        ctx.Contexts[1] = new LamdaNativeContext(x => x.EndsWith("GetProcAddress") ? default : al.GetProcAddress(x));

        ctx = new MultiNativeContext(ALContext.CreateDefaultContext(MojoALLibraryNameContainer.Instance.GetLibraryNames()), null);
        var alc = new ALContext(ctx);
        ctx.Contexts[1] = new LamdaNativeContext( x =>
        {
            if (x.EndsWith("GetProcAddress") ||
                x.EndsWith("GetContextsDevice") ||
                x.EndsWith("GetCurrentContext"))
            {
                return default;
            }

            return (nint)alc.GetProcAddress(alc.GetContextsDevice(alc.GetCurrentContext()), x);
        });

        return (al, alc);
    }
}
