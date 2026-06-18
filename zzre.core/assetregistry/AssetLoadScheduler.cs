using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace zzre;

internal sealed class AssetLoadScheduler : IDisposable
{
    private readonly struct Work(AssetPriority priority, Func<Task> factory) : IComparable<Work>
    {
        public readonly AssetPriority priority = priority;
        public readonly Func<Task> factory = factory;

        public int CompareTo(Work other) => priority.CompareTo(other.priority);
    }

    private bool disposedValue;
    private readonly CancellationTokenSource cancellation;
    private readonly SemaphoreSlim concurrency;
    private readonly Channel<Work> queue = Channel.CreateUnboundedPrioritized<Work>(new()
    {
        AllowSynchronousContinuations = false,
        SingleReader = true,
        SingleWriter = false
    });
    private readonly ChannelWriter<Work> queueWriter;

    public AssetLoadScheduler(CancellationToken cancellation)
        : this(Environment.ProcessorCount, cancellation) { }

    public AssetLoadScheduler(int maxParallel, CancellationToken cancellation)
    {
        queueWriter = queue.Writer;
        this.cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        concurrency = new(maxParallel, maxParallel);
        Task.Run(Receiver, this.cancellation.Token);
    }

    public void Queue(AssetPriority priority, Func<Task> factory)
    {
        queueWriter.TryWrite(new(priority, factory));
    }

    private async Task Receiver()
    {
        var reader = queue.Reader;

        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                await concurrency.WaitAsync(cancellation.Token);
                var work = await reader.ReadAsync(cancellation.Token);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await work.factory();
                    }
                    finally // Ignoring exceptions is fine here
                    {
                        concurrency.Release();
                    }
                }); // no cancellation to not keep a concurrency slot
            }
        }
        catch (Exception e) when (e is OperationCanceledException or TaskCanceledException) { }
    }

    private void Dispose(bool disposing)
    {
        if (disposedValue || !disposing)
            return;
        disposedValue = true;
        cancellation.Cancel();

        queueWriter.Complete();
        concurrency.Dispose();
    }
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
