using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using RemoteryNET;
using RemoteryNET.Pretty;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Veldrid;
using static RemoteryNET.RemoteryPInvoke;

// To make it easier for us the Remotery class and the Remotery bindings package are
// always available to the application with equal interfaces
// The native libraries, the CLI option, the actual functionality however is only
// available if zzre was built with -p:Remotery=true property

namespace zzre;

internal sealed class NullDisposable : IDisposable
{
    private NullDisposable() { }
    public void Dispose() { }
    public static readonly NullDisposable Instance = new();
}

public sealed unsafe class Remotery : IDisposable, ILogEventSink
{
#if REMOTERY
    private const string LoggingOutputTemplateWithoutTime = "[{Level:u3} {SourceContext}] {Message:lj}";

    private readonly ITagContainer diContainer;
    private readonly ILogger logger;
    private readonly MessageTemplateTextFormatter formatter;
    private readonly RemoteryInstance* instance;
    private readonly MemoryStream logMemory;
    private readonly StreamWriter logWriter; // StreamWriter having a second buffer is a bit silly here...
    private readonly Dictionary<string, uint> nameHashCache = new();
    private bool disposedValue;

    public Remotery(ITagContainer diContainer, RemoteryInstance* instance)
    {
        this.diContainer = diContainer;
        this.instance = instance;
        logger = diContainer.GetLoggerFor<Remotery>();
        formatter = new(LoggingOutputTemplateWithoutTime);
        if (instance == null)
        {
            logMemory = null!;
            logWriter = null!;
            disposedValue = true;
        }
        else
        {
            logMemory = new(1024);
            logWriter = new(logMemory, Encoding.UTF8, leaveOpen: true);
        }
    }

    public void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                lock (logWriter)
                {
                    disposedValue = true; // to synchronize potential asynchronous logs
                    logWriter.Dispose();
                    logMemory.Dispose();
                }
            }
            if (instance != null)
            {
                DestroyGlobalInstance(instance);
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public void Emit(LogEvent logEvent)
    {
        if (disposedValue)
            return;
        lock (logWriter)
        {
            if (disposedValue)
                return;
            logMemory.Position = 0;
            formatter.Format(logEvent, logWriter);
            logWriter.Flush();
            logMemory.WriteByte(0);
            fixed (byte* ptr = logMemory.GetBuffer())
                LogText((sbyte*)ptr);
        }
    }

    private struct CPUSampleScope : IDisposable
    {
        private bool wasDisposed;
        public void Dispose()
        {
            if (!wasDisposed)
            {
                wasDisposed = true;
                EndCPUSample();
            }
        }
    }

    public IDisposable SampleCPU(string name, rmtSampleFlags flags = default)
    {
        if (disposedValue)
            return NullDisposable.Instance;
        if (nameHashCache.TryGetValue(name, out var hash))
        {
            BeginCPUSample(name: null, (uint)flags, &hash);
            return new CPUSampleScope();
        }

        var nameBytes = Encoding.UTF8.GetBytes(name);
        uint newHash = 0;
        fixed (byte* namePtr = nameBytes)
            BeginCPUSample((sbyte*)namePtr, (uint)flags, &newHash);
        if (newHash != 0)
            nameHashCache.Add(name, newHash);
        return new CPUSampleScope();
    }

    //public IDisposable SampleGPU(string name, CommandList cl) => NullDisposable.Instance;

#else
    public Remotery(ITagContainer _0, RemoteryInstance* _1) { }
    public void Dispose() { }
    public void Emit(LogEvent _) { }
    public IDisposable SampleCPU(string name, rmtSampleFlags flags = default) => NullDisposable.Instance;
    //public IDisposable SampleGPU(string name, CommandList cl) => NullDisposable.Instance;
#endif
}

unsafe partial class Program
{
#if REMOTERY
    private static Option<ushort> OptionRemoteryPort = new(
        "--remotery-port",
        () => 0,
        "Sets the port for the Remotery server. Use 0 to disable Remotery, 17815 for the default UI port.");

    private static void AddRemoteryOptions(RootCommand command)
    {
        command.AddGlobalOption(OptionRemoteryPort);
    }

    private static Remotery CreateRemotery(ITagContainer diContainer)
    {
        var ctx = diContainer.GetTag<InvocationContext>();
        var port = ctx.ParseResult.GetValueForOption(OptionRemoteryPort);
        if (port == 0)
            return new(diContainer, null);

        var logger = diContainer.GetLoggerFor<Remotery>();
        try
        {
            if (rmtnet_useVulkan() == 0)
                logger.Warning("Remotery was built without Vulkan support. GPU samples will not work");
        }
        catch (DllNotFoundException e)
        {
            logger.Error("Could not load: {Exception}", e);
            return new(diContainer, null);
        }

        var settings = Settings();
        settings->enableThreadSampler = 0;
        settings->reuse_open_port = 1;
        settings->port = port;

        RemoteryInstance* rmt = null;
        rmtError error = CreateGlobalInstance(&rmt);
        if (error != rmtError.RMT_ERROR_NONE || rmt == null)
        {
            logger.Error("Remotery failed to start with reason {Reason}", error);
            return new(diContainer, null);
        }
        var remotery = new Remotery(diContainer, rmt);

        // recreating the logger is fine here as no other component has a reference to a sub-logger
        logger.Information("Started at port {Port}", port);
        diContainer.RemoveTag<ILogger>();
        diContainer.AddTag(CreateLogging(diContainer, remotery));

        return remotery;
    }
#else
    private static void AddRemoteryOptions(RootCommand _) {}
    private static Remotery CreateRemotery(ITagContainer diContainer) => new(diContainer, null);
#endif
}
