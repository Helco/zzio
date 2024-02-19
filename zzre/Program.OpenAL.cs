using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Linq;
using Serilog;
using Serilog.Events;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Loader;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.Enumeration;
using Silk.NET.SDL;
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

    private static readonly Option<string?> OptionSoundDriver = new(
        "--sound-drv",
        () => null,
        "Use a specific sound driver (use two quotes \"\" to print a list)");

    public static void AddSoundOptions(RootCommand command)
    {
        command.AddGlobalOption(OptionNoSound);
        command.AddGlobalOption(OptionSoundDevice);
        command.AddGlobalOption(OptionSoundDriver);
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

        if (!SelectAudioDriver(logger, diContainer))
            return;

        var (al, alc) = LoadOpenALLibraries();
        if (!al.TryGetExtension<Silk.NET.OpenAL.Extensions.EXT.FloatFormat>(out _))
        {
            logger.Error("AL_EXT_FLOAT32 is not present");
            return;
        }

        var deviceName = SelectAudioDevice(logger, alc, invocationContext);
        if (deviceName == null)
            return;
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
        al.ThrowOnError();
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

    private static string? SelectAudioDevice(ILogger logger, ALContext alc, InvocationContext invocationContext)
    {
        if (!alc.TryGetExtension(null, out Enumeration alcEnum))
        {
            logger.Error("ALC_ENUMERATION_EXT is not present");
            return null;
        }

        var userDeviceName = invocationContext.ParseResult.GetValueForOption(OptionSoundDevice);
        var deviceName = alcEnum.GetString(null, GetEnumerationContextString.DefaultDeviceSpecifier);
        var allDeviceNames = alcEnum.GetStringList(GetEnumerationContextStringList.DeviceSpecifiers).ToArray();
        if (allDeviceNames.Length == 0)
            logger.Warning("Did not find any sound device, this will probably fail");
        if (string.IsNullOrWhiteSpace(userDeviceName))
        {
            var outputLevel = userDeviceName is null ? LogEventLevel.Verbose : LogEventLevel.Information;
            if (logger.IsEnabled(outputLevel))
            {
                foreach (var name in allDeviceNames)
                    logger.Write(outputLevel, name == deviceName ? "Default Device: {Name}" : "Device: {Name}", name);
            }
            if (string.IsNullOrEmpty(deviceName))
            {
                logger.Error("No default device specifier was returned ({ErrorCode})", alc.GetError(null));
                return null;
            }
        }
        else
        {
            userDeviceName = userDeviceName.Trim();
            var candidates = new List<string>();
            foreach (var name in allDeviceNames)
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
                foreach (var name in allDeviceNames)
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
        return deviceName;
    }

    private static bool SelectAudioDriver(ILogger logger, ITagContainer diContainer)
    {
        var invocationContext = diContainer.GetTag<InvocationContext>();
        var userDriverName = invocationContext.ParseResult.GetValueForOption(OptionSoundDriver);
        if (userDriverName == null)
            return true;
        userDriverName.Trim();

        var sdl = diContainer.GetTag<Sdl>();
        var numAudioDrivers = sdl.GetNumAudioDrivers();
        if (numAudioDrivers <= 0)
        {
            logger.Error("There were no audio drivers found");
            return false;
        }
        var allDriverNames = Enumerable
            .Range(0, numAudioDrivers)
            .Select(sdl.GetAudioDriverS)
            .NotNull()
            .ToArray();

        bool printAllNames = true;
        string? selectedDriverName = null;
        if (!string.IsNullOrEmpty(userDriverName))
        {
            var candidates = new List<string>();
            foreach (var name in allDriverNames)
            {
                if (name.Equals(userDriverName, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Clear();
                    candidates.Add(name);
                    break;
                }
                if (name.Contains(userDriverName, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(name);
            }

            if (candidates.Count == 0)
                logger.Error("Could not find audio driver {Name}", userDriverName);
            else if (candidates.Count > 1)
            {
                logger.Error("Ambiguous audio driver {Name}", userDriverName);
                foreach (var name in candidates)
                    logger.Information("Driver: {Name}", name);
                printAllNames = false;
            }
            else
            {
                selectedDriverName = candidates[0];
                printAllNames = false;
            }
        }

        if (printAllNames)
        {
            foreach (var name in allDriverNames)
                logger.Information("Driver: {Name}", name);
        }
        if (selectedDriverName != null)
        {
            if (sdl.AudioInit(selectedDriverName) != 0)
            {
                logger.Error("Could not init audio with driver {name}", selectedDriverName);
                return false;
            }
            logger.Information("Selected Driver: {name}", selectedDriverName);
        }

        return true;
    }

    [Conditional("DEBUG")]
    public static void ThrowOnError(this AL al)
    {
        var error = al.GetError();
        if (error != AudioError.NoError)
            throw new InvalidOperationException($"OpenAL returned error {error}");
    }
}
