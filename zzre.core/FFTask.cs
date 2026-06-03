using System;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

internal sealed class FFTask<TResult> : IDisposable where TResult : class, IDisposable
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
        cancelRegistration = ct.Register(() => tcs.TrySetCanceled());
    }

    public Task<TResult> Start()
    {
        if (ct.IsCancellationRequested)
            return Task.FromCanceled<TResult>(ct);
        if (Interlocked.CompareExchange(ref wasStarted, 1, 0) == 0)
            Task.Run(Run); // NO CANCELLATION, it has to run in order to set tcs for every situation
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
                tcs.TrySetResult(task.Result);
            else if (task.IsCanceled)
                tcs.TrySetCanceled();
            else if (task.IsFaulted)
                tcs.TrySetException(task.AsTask().Exception?.InnerException ?? new InvalidOperationException("Unknown failure in asset loading"));
            else
                return task.AsTask().ContinueWith(AfterFactory);
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
            tcs.TrySetResult(task.Result);
        else if (task.IsCanceled)
            tcs.TrySetCanceled();
        else
            tcs.TrySetException(task.Exception?.InnerException ?? new InvalidOperationException("Unknown failure in asset loading"));
    }

    private void Dispose(bool disposing)
    {
        if (disposedValue || !disposing)
            return;
        disposedValue = true;
        cancelRegistration.Dispose();
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
