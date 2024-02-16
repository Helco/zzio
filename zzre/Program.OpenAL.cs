using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using Serilog;
using Serilog.Events;
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

    private static readonly Option<string?> OptionSoundDevice = new(
        "--sound-dev",
        () => null,
        "Use a specific sound device (use two quotes \"\" to print a list)");

    public static void AddSoundOptions(RootCommand command)
    {
        command.AddGlobalOption(OptionNoSound);
        command.AddGlobalOption(OptionSoundDevice);
    }

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

        var userDeviceName = invocationContext.ParseResult.GetValueForOption(OptionSoundDevice);
        var deviceName = alcEnum.GetString(null, GetEnumerationContextString.DefaultDeviceSpecifier);
        if (string.IsNullOrWhiteSpace(userDeviceName))
        {
            var outputLevel = userDeviceName is null ? LogEventLevel.Debug : LogEventLevel.Information;
            if (logger.IsEnabled(outputLevel))
            {
                foreach (var name in alcEnum.GetStringList(GetEnumerationContextStringList.DeviceSpecifiers))
                    logger.Write(outputLevel, name == deviceName ? "Default Device: {Name}" : "Device: {Name}", name);
            }
            if (string.IsNullOrEmpty(deviceName))
            {
                logger.Error("No default device specifier was returned ({ErrorCode})", alc.GetError(null));
                return;
            }
        }
        else
        {
            userDeviceName = userDeviceName.Trim();
            var candidates = new List<string>();
            foreach (var name in alcEnum.GetStringList(GetEnumerationContextStringList.DeviceSpecifiers))
            {
                if (name.Equals(userDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Clear();
                    candidates.Add(name);
                    break;
                }
                if (name.Contains(userDeviceName, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(name);
            }

            if (candidates.Count == 0)
            {
                logger.Error("Could not find device {Name}", userDeviceName);
                foreach (var name in alcEnum.GetStringList(GetEnumerationContextStringList.DeviceSpecifiers))
                    logger.Information(name == deviceName ? "Default Device: {Name}" : "Device: {Name}", name);
                System.Threading.Thread.Sleep(100); // well this is a hack, we write asynchronously to console...
                Environment.Exit(-1); // If the user explicitly requested a device, do not continue if we cannot find any
            }
            else if (candidates.Count > 1)
            {
                logger.Error("Given sound device is ambiguous: {Name}", userDeviceName);
                foreach (var name in candidates)
                    logger.Information("Device: {Name}", name);
                System.Threading.Thread.Sleep(100); 
                Environment.Exit(-1);
            }
            else
                deviceName = candidates[0];
        }

        var device = alc.OpenDevice(deviceName);
        if (device == null)
        {
            logger.Error("Device {DeviceName} could not be opened ({ErrorCode})", deviceName, alc.GetError(null));
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
