using System;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

public enum FFTaskStatus
{
    Running,
    Success,
    Error,
    Canceled
}

/// <summary>
/// A fire-and-forget task, status and exception handling is always deferred
/// </summary>
public sealed class FFTask
{
    private readonly Task task;
    private readonly CancellationToken ct;
    private int status;
    private Exception? exception;
    
    public FFTaskStatus Status
    {
        get => (FFTaskStatus)status;
    }

    public Exception? Exception => exception;

    public FFTask(Func<Task> func, CancellationToken ct)
    {
        this.ct = ct;
        if (ct.IsCancellationRequested)
        {
            status = (int)FFTaskStatus.Canceled;
            task = Task.FromCanceled(ct);
        }
        else
            task = Task.Run(() => FrameTask(func), ct);
    }

    public ValueTask<FFTaskStatus> Completion => Status is not FFTaskStatus.Running
        ? ValueTask.FromResult(Status)
        : WaitForCompletion();

    private async ValueTask<FFTaskStatus> WaitForCompletion()
    {
        await task;
        return Status;
    }

    private async Task FrameTask(Func<Task> func)
    {
        try
        {
            await func().WaitAsync(ct).ConfigureAwait(false);
            Interlocked.CompareExchange(ref status, (int)FFTaskStatus.Success, (int)FFTaskStatus.Running);
        }
        catch (OperationCanceledException)
        {
            Interlocked.CompareExchange(ref status, (int)FFTaskStatus.Canceled, (int)FFTaskStatus.Running);
        }
        catch (Exception e)
        {
            Interlocked.Exchange(ref exception, e);
            Interlocked.Exchange(ref status, (int)FFTaskStatus.Error);
        }
    }
}
