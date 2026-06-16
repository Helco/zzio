using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace zzre.tests;

[TestFixture, CancelAfter(1000)]
public class TestFFTask
{
    public const int RepeatCount = 100;

    public sealed record IntResult(int Value) : IDisposable
    {
        public void Dispose() { }
    }

    public sealed class IsDisposedResult : IDisposable
    {
        public bool IsDisposed;

        public void Dispose()
        {
            Assert.That(IsDisposed, Is.False, "Result was already disposed");
            IsDisposed = true;
        }
    }

    public sealed class WaitResult : IDisposable
    {
        public readonly TaskCompletionSource ForTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource ForDispose = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource OnTaskStarted = new();
        public Task WaitForDispose(CancellationToken ct) =>
            ForDispose.Task.WaitAsync(ct);
        public bool WasCalled => ForTask.Task.IsCompletedSuccessfully;

        public void Dispose() => ForDispose.TrySetResult();
        public async ValueTask<WaitResult> Factory()
        {
            OnTaskStarted.SetResult();
            await ForTask.Task;
            return this;
        }
    }

    private static ValueTask<IntResult> GetFourtyTwo() =>
        ValueTask.FromResult<IntResult>(new(42));

    [Test, Repeat(RepeatCount, true)]
    public async Task WaitTask(CancellationToken ct)
    {
        using var t = new FFTask<IntResult>(GetFourtyTwo, ct);
        var result = await t.WaitTask;
        Assert.That(result.Value, Is.EqualTo(42));
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task ObserverTask(CancellationToken ct)
    {
        using var t = new FFTask<IntResult>(GetFourtyTwo, ct);
        _ = t.Start();
        var result = await t.ObserverTask;
        Assert.That(result.Value, Is.EqualTo(42));
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task FactoryIsCalledOnce(CancellationToken ct)
    {
        int counter = 0;
        using var t = new FFTask<IntResult>(() =>
        {
            Interlocked.Increment(ref counter);
            return ValueTask.FromResult(new IntResult(42));
        }, ct);

        await t.WaitTask;
        await t.WaitTask;
        await t.Start();
        await t.Start();
        await t.ObserverTask;
        await t.ObserverTask;
        t.Dispose();
        t.Dispose();
        Assert.That(Interlocked.CompareExchange(ref counter, 0, 0), Is.EqualTo(1));
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task FactoryIsCalledLazy(CancellationToken ct)
    {
        using var t = new FFTask<IntResult>(() =>
        {
            Assert.Fail("Factory was called unexpectedly");
            return ValueTask.FromResult(new IntResult(42));
        }, ct);

        _ = t.ObserverTask;
        _ = t.ObserverTask;
        t.Dispose();
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task DisposeOnce(CancellationToken ct)
    {
        var result = new IsDisposedResult();
        using var t = new FFTask<IsDisposedResult>(() => ValueTask.FromResult(result), ct);
        await t.WaitTask;
        Assert.That(result.IsDisposed, Is.False);
        await t.WaitTask;
        Assert.That(result.IsDisposed, Is.False);
        t.Dispose();
        Assert.That(result.IsDisposed, Is.True);
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task DisposeBefore(CancellationToken ct)
    {
        using var t = new FFTask<IntResult>(GetFourtyTwo, ct);
        t.Dispose();
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task DisposeAfter(CancellationToken ct)
    {
        var result = new WaitResult();
        result.ForTask.SetResult();
        using var t = new FFTask<WaitResult>(result.Factory, ct);
        var waitTask = t.WaitTask;
        t.Dispose();

        if (result.WasCalled)
            await result.WaitForDispose(ct);
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task DisposeDuring(CancellationToken ct)
    {
        var result = new WaitResult();
        using var t = new FFTask<WaitResult>(result.Factory, ct);
        t.Start();

        await result.OnTaskStarted.Task;
        t.Dispose();
        result.ForTask.SetResult();

        await result.WaitForDispose(ct);
    }

    private static CancellationTokenSource cancelledSource = new(0);
    private static CancellationToken CancelledToken => cancelledSource.Token;

    [Test, Repeat(RepeatCount, true)]
    public async Task CancelBeforeStart(CancellationToken ct)
    {
        using var t = new FFTask<IntResult>(GetFourtyTwo, CancelledToken);
        await Assert.ThatAsync(t.Start, Throws.InstanceOf<OperationCanceledException>());
        await Assert.ThatAsync(() => t.WaitTask, Throws.InstanceOf<OperationCanceledException>());
        await Assert.ThatAsync(() => t.ObserverTask, Throws.InstanceOf<OperationCanceledException>());
        t.Dispose();
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task CancelAfter(CancellationToken ct)
    {
        var result = new WaitResult();
        result.ForTask.SetResult();
        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var t = new FFTask<WaitResult>(result.Factory, tcs.Token);
        await t.WaitTask;

        tcs.Cancel();
        await t.ObserverTask; // none of these should throw as we already have the result
        await t.WaitTask;
        await t.Start();
        t.Dispose();
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task CancelDuring(CancellationToken ct)
    {
        var result = new WaitResult();
        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var t = new FFTask<WaitResult>(result.Factory, tcs.Token);
        t.Start();

        await result.OnTaskStarted.Task;
        tcs.Cancel();
        result.ForTask.SetResult();

        // The cancellation can happen or be to late,
        // but if it does happen, the disposal should happen as well
        try
        {
            await t.ObserverTask;
            await t.WaitTask;
            await t.Start();
        }
        catch(TaskCanceledException)
        {
            await result.WaitForDispose(ct);
        }
        t.Dispose();
    }

    private sealed class TestException : Exception { }

    [Test, Repeat(RepeatCount, true)]
    public async Task FactoryFailsSync(CancellationToken ct)
    {
        var result = new WaitResult();
        result.ForTask.SetException(new TestException());
        using var t = new FFTask<WaitResult>(result.Factory, ct);

        t.Start(); // start should never throw synchronously

        await Assert.ThatAsync(() => t.ObserverTask, Throws.InstanceOf<TestException>());
        await Assert.ThatAsync(() => t.WaitTask, Throws.InstanceOf<TestException>());
        await Assert.ThatAsync(t.Start, Throws.InstanceOf<TestException>());
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task FactoryFailsAsync(CancellationToken ct)
    {
        var result = new WaitResult();
        using var t = new FFTask<WaitResult>(result.Factory, ct);
        t.Start();
        await result.OnTaskStarted.Task;

        result.ForTask.SetException(new TestException());
        await Assert.ThatAsync(() => t.ObserverTask, Throws.InstanceOf<TestException>());
        await Assert.ThatAsync(() => t.WaitTask, Throws.InstanceOf<TestException>());
        await Assert.ThatAsync(t.Start, Throws.InstanceOf<TestException>());
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task FactoryIsCancelledSync(CancellationToken ct)
    {
        var result = new WaitResult();
        result.ForTask.SetCanceled(CancelledToken);
        using var t = new FFTask<WaitResult>(result.Factory, ct);

        t.Start(); // start should never throw synchronously

        await Assert.ThatAsync(() => t.ObserverTask, Throws.InstanceOf<TaskCanceledException>());
        await Assert.ThatAsync(() => t.WaitTask, Throws.InstanceOf<TaskCanceledException>());
        await Assert.ThatAsync(t.Start, Throws.InstanceOf<TaskCanceledException>());
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task FactoryIsCancelledAsync(CancellationToken ct)
    {
        var result = new WaitResult();
        using var t = new FFTask<WaitResult>(result.Factory, ct);
        t.Start();
        await result.OnTaskStarted.Task;

        result.ForTask.SetCanceled(CancelledToken);
        await Assert.ThatAsync(() => t.ObserverTask, Throws.InstanceOf<TaskCanceledException>());
        await Assert.ThatAsync(() => t.WaitTask, Throws.InstanceOf<TaskCanceledException>());
        await Assert.ThatAsync(t.Start, Throws.InstanceOf<TaskCanceledException>());
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task StressRun(CancellationToken ct)
    {
        const int count = 100;
        var tasks = Enumerable.Repeat(0, count).Select(_ =>
            new FFTask<IntResult>(GetFourtyTwo, ct))
            .ToArray();

        await Task.WhenAll(tasks.Select(t => t.WaitTask)).WaitAsync(ct);

        foreach (var t in tasks)
            t.Dispose();
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task StressCancel(CancellationToken ct)
    {
        const int count = 100;
        var tasks = Enumerable.Repeat(0, count).Select(_ =>
        {
            var result = new WaitResult();
            result.ForTask.SetCanceled(CancelledToken);
            return new FFTask<WaitResult>(result.Factory, ct);
        }).ToArray();

        await Assert.ThatAsync(
            () => Task.WhenAll(tasks.Select(t => t.WaitTask)).WaitAsync(ct),
            Throws.InstanceOf<TaskCanceledException>());

        foreach (var t in tasks)
            t.Dispose();
    }

    // ---

    [Test, Repeat(RepeatCount, true)]
    public async Task ConcurrentStartAndCancellation(CancellationToken ct)
    {
        var result = new WaitResult();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var t = new FFTask<WaitResult>(result.Factory, cts.Token);

        // Race: Start and cancel simultaneously
        var startTask = Task.Run(() => t.Start(), ct);
        var cancelTask = Task.Run(() => cts.Cancel(), ct);

        try
        {
            await Task.WhenAll(startTask, cancelTask).WaitAsync(ct);
        }
        catch(TaskCanceledException)
        {
            // this is okay, the cancellation has won the race
        }
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task ConcurrentStartAndDispose(CancellationToken ct)
    {
        var result = new WaitResult();
        result.ForTask.SetResult();
        using var t = new FFTask<WaitResult>(result.Factory, ct);

        var startTask = Task.Run(() => t.Start());
        var disposeTask = Task.Run(() => t.Dispose());

        try
        {
            await Task.WhenAll(startTask, disposeTask).WaitAsync(ct);
        }
        catch(ObjectDisposedException)
        {
            // this is okay, the disposal has won the race
        }
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task FactoryThrowsDirectly(CancellationToken ct)
    {
        using var t = new FFTask<IntResult>(() =>
            throw new TestException(), ct);

        await Assert.ThatAsync(() => t.WaitTask, Throws.InstanceOf<TestException>());
    }

    [Test, Repeat(RepeatCount, true)]
    public async Task CancellationRegistrationRace(CancellationToken ct)
    {
        // This tests the race between cancellation registration and Start()
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = new List<Task>();

        for (int i = 0; i < 1000; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var t = new FFTask<IntResult>(GetFourtyTwo, cts.Token);

                // Race: cancel immediately after construction
                if (i % 2 == 0) cts.Cancel();

                try { await t.WaitTask; }
                catch (OperationCanceledException) { /* Expected */ }
            }));
        }

        await Task.WhenAll(tasks).WaitAsync(ct);
    }

    [Test]
    public async Task MixedConcurrentOperations(CancellationToken ct)
    {
        const int taskCount = 1000;
        const int operationsPerTask = 10;

        var tasks = new List<Task>();
        var ffTasks = Enumerable.Repeat(0, taskCount).Select(_ =>
            new FFTask<IntResult>(GetFourtyTwo, ct))
            .ToArray();

        for (int taskI = 0; taskI < taskCount; taskI++)
        {
            var taskIndex = taskI;
            tasks.Add(Task.Run(async () =>
            {
                var operations = new List<Task>();
                for (int opI = 0; opI < operationsPerTask; opI++)
                {
                    operations.Add(Task.Run(async () =>
                    {
                        try
                        {
                            switch (opI % 4)
                            {
                                case 0: await ffTasks[taskIndex].WaitTask; break;
                                case 1: await ffTasks[taskIndex].Start(); break;
                                case 2: _ = ffTasks[taskIndex].ObserverTask; break;
                                case 3: ffTasks[taskIndex].Dispose(); break;
                            }
                        }
                        catch { /* Expected in stress test */ }
                    }));
                }
                await Task.WhenAll(operations).WaitAsync(ct);
            }));
        }

        await Task.WhenAll(tasks).WaitAsync(ct);

        foreach (var t in ffTasks)
            t.Dispose();
    }
}
