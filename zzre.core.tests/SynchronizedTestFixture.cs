using System;
using System.Reflection;
using System.Threading;
using NUnit.Framework;

// as workaround to STA not being supported on Linux
// from: https://github.com/nunit/nunit/issues/4110

public abstract class SynchronizedTestFixture
{
    private SynchronizationContext? _previousContext;
    private SynchronizationContext? _ourContext;

    [SetUp]
    public void SynchronizedSetup()
    {
        _previousContext = SynchronizationContext.Current;
        _ourContext = CreateNUnitSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(_ourContext);
    }

    [TearDown]
    public void SynchronizedTeardown()
    {
        SynchronizationContext.SetSynchronizationContext(_previousContext);
    }

    private static SynchronizationContext CreateNUnitSynchronizationContext()
    {
        Type type = typeof(Assert).Assembly.GetType("NUnit.Framework.Internal.SingleThreadedTestSynchronizationContext")!;

        return (SynchronizationContext)Activator.CreateInstance(type,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
            null,
            [new TimeSpan(TimeSpan.TicksPerSecond)],
            null)!;
    }
}
