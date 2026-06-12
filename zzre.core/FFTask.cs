using System;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

public sealed class FFTask<TResult> : IDisposable where TResult : class, IDisposable
{
    private bool disposedValue;
    private int wasStarted;
    private readonly Func<ValueTask<TResult>> factory;
    private readonly CancellationToken ct;
    private readonly CancellationTokenRegistration cancelRegistration;
    private readonly TaskCompletionSource<TResult> tcs = new();

    public Task<TResult> ObserverTask => tcs.Task;
    public Task<TResult> WaitTask => Start();

    public FFTask(Func<ValueTask<TResult>> factory, CancellationToken ct)
    {
        this.factory = factory;
        this.ct = ct;
        cancelRegistration = ct.Register(() =>
        {
            // only cancel if we did not start yet. Otherwise we might 
            // miss the result being produced successfullly
            // the started Task will set tcs in any case
            if (Interlocked.CompareExchange(ref wasStarted, 0, 0) == 0)
                tcs.TrySetCanceled();
        });
    }

    public Task<TResult> Start()
    {
        if (Interlocked.CompareExchange(ref wasStarted, 1, 0) == 0)
        {
            if (ct.IsCancellationRequested)
                tcs.TrySetCanceled(ct);
            else
                Task.Run(Run); // NO CANCELLATION PASSED, it has to run in order to set tcs for every situation
        }
        return tcs.Task;
    }

    private Task Run()
    {
        if (ct.IsCancellationRequested)
        {
            tcs.TrySetCanceled();
            return Task.CompletedTask;
        }
        try
        {
            var task = factory();
            if (task.IsCompletedSuccessfully)
            {
                var result = task.Result;
                if (!tcs.TrySetResult(result))
                    result?.Dispose();
            }
            else if (task.IsCanceled)
                tcs.TrySetCanceled();
            else if (task.IsFaulted)
                tcs.TrySetException(task.AsTask().Exception?.InnerException ?? new InvalidOperationException("Unknown failure in asset loading"));
            else
                return task.AsTask().ContinueWith(AfterFactory).WaitAsync(ct).ContinueWith(AfterFactoryWait);
        }
        catch(OperationCanceledException ex)
        {
            tcs.TrySetCanceled(ex.CancellationToken);
        }
        catch(Exception ex)
        {
            tcs.TrySetException(ex);
        }
        return Task.CompletedTask;
    }

    private void AfterFactory(Task<TResult> task)
    {
        if (task.IsCompletedSuccessfully)
        {
            var result = task.Result;
            if (!tcs.TrySetResult(task.Result))
                result?.Dispose();
        }
        else if (task.IsCanceled)
            tcs.TrySetCanceled();
        else
            tcs.TrySetException(task.Exception?.InnerException ?? new InvalidOperationException("Unknown failure in asset loading"));
    }

    private void AfterFactoryWait(Task task)
    {
        // This function cannot handle the result, but the only instance
        // where it is called without AfterFactory being called would be 
        // some fault (probably even only cancellation)
        if (task.IsCanceled)
            tcs.TrySetCanceled();
        else if (task.IsFaulted)
            tcs.TrySetException(task.Exception?.InnerException ?? new InvalidOperationException("Unknown failure in asset loading"));
    }

    private void Dispose(bool disposing)
    {
        if (disposedValue || !disposing)
            return;
        disposedValue = true;
        cancelRegistration.Dispose();
        Interlocked.Exchange(ref wasStarted, 1); // prevents start after disposal

        // We dispose of the result. (maybe a future API should be able to prevent this)
        tcs.Task.ContinueWith(
            t => t.Result?.Dispose(),
            TaskContinuationOptions.OnlyOnRanToCompletion |
            TaskContinuationOptions.ExecuteSynchronously);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
