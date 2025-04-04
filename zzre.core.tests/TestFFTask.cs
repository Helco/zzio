using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace zzre.tests;

[TestFixture]
[CancelAfter(500)]
public class TestFFTask
{
    public const int MaxTime = 700;

    private Exception TestException() => new IOException("Something is wrong");

    [Test, MaxTime(MaxTime)]
    public async Task PreCompletedTask(CancellationToken ct)
    {
        var ff = new FFTask(() => Task.CompletedTask, ct);
        await ff.Completion;
        Assert.That(ff.Exception, Is.Null);
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Success));
    }

    [Test, MaxTime(MaxTime)]
    public async Task PreCancelledTask2(CancellationToken ct)
    {
        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tcs.Cancel();
        var ff = new FFTask(() => Task.FromCanceled(tcs.Token), ct); // mind using the global cancellation token here
        await ff.Completion;
        Assert.That(ff.Exception, Is.Null);
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Canceled));
    }

    [Test, MaxTime(MaxTime)]
    public async Task PreCancelledTask(CancellationToken ct)
    {
        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tcs.Cancel();
        var ff = new FFTask(() => Task.FromCanceled(tcs.Token), tcs.Token); // mind using the local cancellation token here
        await ff.Completion;
        Assert.That(ff.Exception, Is.Null);
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Canceled));
    }

    [Test, MaxTime(MaxTime)]
    public async Task PreException(CancellationToken ct)
    {
        var ff = new FFTask(() => Task.FromException(TestException()), ct);
        await ff.Completion;
        Assert.That(ff.Exception, Is.InstanceOf<IOException>());
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Error));
    }

    [Test, MaxTime(MaxTime)]
    public async Task ImmCompletedTask(CancellationToken ct)
    {
        var ff = new FFTask(async () => {}, ct);
        await ff.Completion;
        Assert.That(ff.Exception, Is.Null);
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Success));
    }

    [Test, MaxTime(MaxTime)]
    public async Task ImmCancelledTask(CancellationToken ct)
    {
        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ff = new FFTask(async () =>
        {
            tcs.Cancel();
            tcs.Token.ThrowIfCancellationRequested();
        }, tcs.Token); // mind using the local cancellation token here
        await ff.Completion;
        Assert.That(ff.Exception, Is.Null);
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Canceled));
    }

    [Test, MaxTime(MaxTime)]
    public async Task ImmException(CancellationToken ct)
    {
        var ff = new FFTask(async() => throw TestException(), ct);
        await ff.Completion;
        Assert.That(ff.Exception, Is.InstanceOf<IOException>());
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Error));
    }

    [Test, MaxTime(MaxTime)]
    public async Task YieldCompletedTask(CancellationToken ct)
    {
        var ff = new FFTask(async () =>
        {
            await Task.Yield();
        }, ct);
        await ff.Completion;
        Assert.That(ff.Exception, Is.Null);
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Success));
    }

    [Test, MaxTime(MaxTime)]
    public async Task YieldCancelledTask(CancellationToken ct)
    {
        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ff = new FFTask(async () =>
        {
            await Task.Yield();
            tcs.Cancel();
            tcs.Token.ThrowIfCancellationRequested();
        }, tcs.Token); // mind using the local cancellation token here
        await ff.Completion;
        Assert.That(ff.Exception, Is.Null);
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Canceled));
    }

    [Test, MaxTime(MaxTime)]
    public async Task YieldException(CancellationToken ct)
    {
        var ff = new FFTask(async() => 
        {
            await Task.Yield();
            throw TestException();
        }, ct);
        await ff.Completion;
        Assert.That(ff.Exception, Is.InstanceOf<IOException>());
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Error));
    }
    
    private async Task<int> TestWork()
    {
        await Task.Delay(2);
        int a = 0;
        for (int i = 0; i < 1000; i++) {
            unchecked
            {
                a = (a * a) + i;
            }
        }
        await Task.Delay(3);
        return a;
    }

    [Test, MaxTime(MaxTime)]
    public async Task WorkCompletedTask(CancellationToken ct)
    {
        var ff = new FFTask(async () =>
        {
            await TestWork();
        }, ct);
        await ff.Completion;
        Assert.That(ff.Exception, Is.Null);
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Success));
    }

    [Test, MaxTime(MaxTime)]
    public async Task WorkCancelledTask(CancellationToken ct)
    {
        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ff = new FFTask(async () =>
        {
            await TestWork();
            tcs.Cancel();
            tcs.Token.ThrowIfCancellationRequested();
        }, tcs.Token); // mind using the local cancellation token here
        await ff.Completion;
        Assert.That(ff.Exception, Is.Null);
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Canceled));
    }

    [Test, MaxTime(MaxTime)]
    public async Task WorkException(CancellationToken ct)
    {
        var ff = new FFTask(async() => 
        {
            await TestWork();
            throw TestException();
        }, ct);
        await ff.Completion;
        Assert.That(ff.Exception, Is.InstanceOf<IOException>());
        Assert.That(ff.Status, Is.EqualTo(FFTaskStatus.Error));
    }
}
