using System;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

public enum FFTaskStatus
{
    Created,
    Running,
    Success,
    Error,
    Canceled
}

/// <summary>
/// A fire-and-forget task, status and exception handling is always deferred
/// </summary>
public sealed class FFTask(Func<Task> func, CancellationToken ct)
{
    private readonly Func<Task> func = func;
    private readonly CancellationToken ct = ct;
    private int status = (int)FFTaskStatus.Created;
    private Task<FFTaskStatus>? task;
    private Exception? exception;
    
    public FFTaskStatus Status
    {
        get => (FFTaskStatus)status;
    }

    public bool IsCompleted => Status is not (FFTaskStatus.Created or FFTaskStatus.Running);

    public Exception? Exception => exception;

    public ValueTask<FFTaskStatus> Completion
    {
        get
        {
            var status = (FFTaskStatus)Interlocked.CompareExchange(ref this.status, (int)FFTaskStatus.Running, (int)FFTaskStatus.Created);
            if (status is FFTaskStatus.Created)
            {
                // the status *now* is Running and we are responsible for starting it
                if (ct.IsCancellationRequested)
                {
                    // but we were already cancelled
                    this.status = (int)FFTaskStatus.Canceled;
                    task = Task.FromResult(FFTaskStatus.Canceled);
                    return ValueTask.FromResult(FFTaskStatus.Canceled);
                }
                else
                    task = Task.Run(() => FrameTask(func), ct);
                status = FFTaskStatus.Running;
            }
            return status is not FFTaskStatus.Running
                ? ValueTask.FromResult(Status)
                : WaitForCompletion();
        }
    }

    private async ValueTask<FFTaskStatus> WaitForCompletion()
    {
        await task!;
        return Status;
    }

    private async Task<FFTaskStatus> FrameTask(Func<Task> func)
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
        return (FFTaskStatus)status;
    }
}
