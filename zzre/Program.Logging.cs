using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;

namespace zzre; 

partial class Program
{
    private const string LoggingOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";

#if DEBUG
    private static readonly LogEventLevel DefaultLogLevel = LogEventLevel.Debug;
#else
    private static readonly LogEventLevel DefaultLogLevel = LogEventLevel.Information;
#endif

    private static readonly Option<LogEventLevel> OptionLogLevel = new(
        new[] { "--log-level" },
        () => DefaultLogLevel,
        "Sets the minimum level for logging");

    private const string DefaultLogFilePath = "./zzre.log";
    private static readonly Option<FileInfo> OptionLogFilePath = new Option<FileInfo>(
        new[] { "--log-file-path" },
        () => new(DefaultLogFilePath),
        "Sets the path of the log file")
        .LegalFilePathsOnly();

    private static readonly Option<string[]> OptionLogOverrides = new(
        new[] { "--log" },
        () => Array.Empty<string>(),
        "Overrides the minimum level for a single log source (use \"Source=Level\")");

    private static void AddLoggingOptions(RootCommand command)
    {
        OptionLogOverrides.AddValidator(r => TryParseLogOverride(r.Token?.Value, out _, out _));
        command.AddGlobalOption(OptionLogLevel);
        command.AddGlobalOption(OptionLogFilePath);
        command.AddGlobalOption(OptionLogOverrides);
    }

    private static ILogger CreateLogging(InvocationContext ctx)
    {
        var config = new LoggerConfiguration()
            .MinimumLevel.Is(ctx.ParseResult.GetValueForOption(OptionLogLevel))
            .WriteTo.Async(wt => wt.Console(outputTemplate: LoggingOutputTemplate));

        var filePath = ctx.ParseResult.GetValueForOption(OptionLogFilePath);
        if (filePath is not null)
            config = config.WriteTo.File(filePath.FullName, outputTemplate: LoggingOutputTemplate);

        var overrides = ctx.ParseResult.GetValueForOption(OptionLogOverrides) ?? Array.Empty<string>();
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

    // Let's not log the entire namespace chain, the class name should suffice
    public static ILogger For<T>(this ILogger parent) =>
        parent.ForContext("SourceContext", typeof(T).Name);

    public static ILogger GetLoggerFor<T>(this ITagContainer diContainer) =>
        diContainer.GetTag<ILogger>().For<T>();
}
