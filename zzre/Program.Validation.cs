using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;
using zzre.validation;

namespace zzre;

internal partial class Program
{
    private static readonly Option<ushort> OptionValidationConcurrency = new(
        "--concurrency",
        () => checked((ushort)Environment.ProcessorCount),
        "Max number of parallel validation tasks"
    );

    private static void AddValidationCommand(RootCommand parent)
    {
        var command = new Command("validate",
            "Starts an automated validation of assets, i.e. the capability of zzre to load and process them.");
        command.AddOption(OptionValidationConcurrency);
        command.SetHandler(HandleValidation);
        parent.AddCommand(command);
    }

    private static void HandleValidation(InvocationContext ctx)
    {
        var diContainer = CommonStartupBeforeWindow(ctx);
        CommonStartupAfterWindow(diContainer);

        using var cancellationSource = new CancellationTokenSource();
        var validator = new Validator(diContainer);
        Task.Run(async () =>
        {
            WriteConsoleLine("Validation: starting...");
            Console.CancelKeyPress += (_0, _1) => cancellationSource.Cancel();
            var validationTask = Task.Run(() => validator.Run(cancellationSource.Token));
            while (!validationTask.IsCompleted)
            {
                WriteProgress();
                await Task.Delay(500, cancellationSource.Token);
            }
        }, cancellationSource.Token).WaitAndRethrow();
        WriteProgress();

        CommonCleanup(diContainer);

        void WriteProgress() =>
            WriteConsoleLine($"Validation: {validator.ProcessedFileCount:D8} processed / {validator.QueuedFileCount:D8} queued ({validator.FaultyFileCount} faulty)");

        static void WriteConsoleLine(string line)
        {
            lock (consoleLock)
            {
                if (Console.IsErrorRedirected)
                {
                    Console.Error.WriteLine(line);
                    return;
                }
                var (prevLeft, prevTop) = Console.GetCursorPosition();
                Console.SetCursorPosition(0, 0);
                Console.Error.Write(line);
                Console.Error.Write(new string(' ', Console.WindowWidth - line.Length));
                if (prevTop == 0)
                {
                    prevLeft = 0;
                    prevTop = 1;
                }
                Console.SetCursorPosition(prevLeft, prevTop);
            }
        }
    }
}
