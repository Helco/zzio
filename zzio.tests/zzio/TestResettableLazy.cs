using NUnit.Framework;

namespace zzio.tests;

public class TestResettableLazy
{
    [Test]
    public void IsEmptyByDefault()
    {
        var refLazy = new ResettableLazy<string>(() => "");
        Assert.IsFalse(refLazy.HasValue);

        var valLazy = new ResettableLazyValue<int>(() => 0);
        Assert.IsFalse(valLazy.HasValue);
    }

    [Test]
    public void IsNotEmptyAfterAccess()
    {
        var refLazy = new ResettableLazy<string>(() => "");
        _ = refLazy.Value;
        Assert.IsTrue(refLazy.HasValue);

        var valLazy = new ResettableLazyValue<int>(() => 0);
        _ = valLazy.Value;
        Assert.IsTrue(valLazy.HasValue);
    }

    [Test]
    public void CanBeInitialized()
    {
        var refLazy = new ResettableLazy<string>(() => "", "Hello World");
        Assert.IsTrue(refLazy.HasValue);
        Assert.AreEqual("Hello World", refLazy.Value);

        var valLazy = new ResettableLazyValue<int>(() => 0, 42);
        Assert.IsTrue(valLazy.HasValue);
        Assert.AreEqual(valLazy.Value, 42);
    }

    [Test]
    public void DoesNotCallCreatorEarly()
    {
        bool didCallRef = false;
        var refLazy = new ResettableLazy<string>(() => { didCallRef = true; return ""; });
        Assert.IsFalse(didCallRef);
        _ = refLazy.Value;
        Assert.IsTrue(didCallRef);

        bool didCallVal = false;
        var valLazy = new ResettableLazyValue<int>(() => { didCallVal = true; return 0; });
        Assert.IsFalse(didCallVal);
        _ = valLazy.Value;
        Assert.IsTrue(didCallVal);
    }

    [Test]
    public void CallsCreatorOnlyOnce()
    {
        int callCountRef = 0;
        var refLazy = new ResettableLazy<string>(() => { callCountRef++; return ""; });
        _ = refLazy.Value;
        _ = refLazy.Value;
        _ = refLazy.Value;
        Assert.AreEqual(1, callCountRef);

        int callCountVal = 0;
        var valLazy = new ResettableLazyValue<int>(() => { callCountVal++; return 0; });
        _ = valLazy.Value;
        _ = valLazy.Value;
        _ = valLazy.Value;
        Assert.AreEqual(1, callCountVal);
    }

    [Test]
    public void TakesValueFromCreator()
    {
        var refLazy = new ResettableLazy<string>(() => "Hello World");
        Assert.AreEqual("Hello World", refLazy.Value);

        var valLazy = new ResettableLazyValue<int>(() => 42);
        Assert.AreEqual(42, valLazy.Value);
    }

    [Test]
    public void CanBeReset()
    {
        var refLazy = new ResettableLazy<string>(() => "");
        _ = refLazy.Value;
        refLazy.Reset();
        Assert.IsFalse(refLazy.HasValue);

        var valLazy = new ResettableLazyValue<int>(() => 0);
        _ = valLazy.Value;
        valLazy.Reset();
        Assert.IsFalse(valLazy.HasValue);
    }

    [Test]
    public void UsesCreatorAfterReset()
    {
        bool didCallRef = false;
        var refLazy = new ResettableLazy<string>(() =>
        {
            if (didCallRef)
                return "Second";
            didCallRef = true;
            return "First";
        });
        _ = refLazy.Value;
        refLazy.Reset();
        Assert.AreEqual("Second", refLazy.Value);

        bool didCallVal = false;
        var valLazy = new ResettableLazyValue<int>(() =>
        {
            if (didCallVal)
                return 1337;
            didCallVal = true;
            return 42;
        });
        _ = valLazy.Value;
        valLazy.Reset();
        Assert.AreEqual(1337, valLazy.Value);
    }
}
