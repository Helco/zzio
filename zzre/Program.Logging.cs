using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace zzre;

internal partial class Program
{
    private const string LoggingOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";

#if DEBUG
    private static readonly LogEventLevel DefaultLogLevel = LogEventLevel.Debug;
#else
    private static readonly LogEventLevel DefaultLogLevel = LogEventLevel.Information;
#endif

    private static readonly Option<LogEventLevel> OptionLogLevel = new(
        ["--log-level"],
        () => DefaultLogLevel,
        "Sets the minimum level for logging");

    private const string DefaultLogFilePath = "./zzre.log";
    private static readonly Option<FileInfo> OptionLogFilePath = new Option<FileInfo>(
        ["--log-file-path"],
        () => new(DefaultLogFilePath),
        "Sets the path of the log file")
        .LegalFilePathsOnly();

    private static readonly Option<string[]> OptionLogOverrides = new(
        ["--log"],
        Array.Empty<string>,
        "Overrides the minimum level for a single log source (use \"Source=Level\")");

    private static readonly object consoleLock = new();

    private static void AddLoggingOptions(RootCommand command)
    {
        OptionLogOverrides.AddValidator(r => TryParseLogOverride(r.Token?.Value, out _, out _));
        command.AddGlobalOption(OptionLogLevel);
        command.AddGlobalOption(OptionLogFilePath);
        command.AddGlobalOption(OptionLogOverrides);
    }

    private static ILogger CreateLogging(ITagContainer diContainer, ILogEventSink? additionalSink = null)
    {
        var ctx = diContainer.GetTag<InvocationContext>();
        var config = new LoggerConfiguration()
            .MinimumLevel.Is(ctx.ParseResult.GetValueForOption(OptionLogLevel))
            .WriteTo.Async(wt => wt.Console(outputTemplate: LoggingOutputTemplate, syncRoot: consoleLock));

        var filePath = ctx.ParseResult.GetValueForOption(OptionLogFilePath);
        if (filePath is not null)
            config = config.WriteTo.File(filePath.FullName, outputTemplate: LoggingOutputTemplate);

        if (additionalSink != null)
            config.WriteTo.Sink(additionalSink);

        var overrides = ctx.ParseResult.GetValueForOption(OptionLogOverrides) ?? [];
        foreach (var @override in overrides)
        {
            if (TryParseLogOverride(@override, out var source, out var level))
                config = config.MinimumLevel.Override(source, level);
        }

        var logger = config
            .Enrich.FromLogContext()
            .CreateLogger();
        var assembly = typeof(Program).Assembly.GetName();
        logger.Information($"{assembly.Name} {assembly.Version} {ThisAssembly.Git.Commit} ({ThisAssembly.Git.CommitDate})");
        return logger;
    }

    private static bool TryParseLogOverride(string? value, out string source, out LogEventLevel level)
    {
        source = "";
        level = default;
        var assignI = value?.IndexOf('=') ?? -1;
        if (value is null || assignI < 1 || assignI + 1 == value.Length)
            return false;
        source = value[..assignI].Trim();
        var levelString = value[(assignI + 1)..].Trim();
        return
            !string.IsNullOrWhiteSpace(source) &&
            Enum.TryParse(levelString, ignoreCase: true, out level);
    }
}
