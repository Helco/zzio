using NUnit.Framework;

namespace zzio.tests;

public class TestResettableLazy
{
    [Test]
    public void IsEmptyByDefault()
    {
        var refLazy = new ResettableLazy<string>(() => "");
        Assert.That(refLazy.HasValue, Is.False);

        var valLazy = new ResettableLazyValue<int>(() => 0);
        Assert.That(valLazy.HasValue, Is.False);
    }

    [Test]
    public void IsNotEmptyAfterAccess()
    {
        var refLazy = new ResettableLazy<string>(() => "");
        _ = refLazy.Value;
        Assert.That(refLazy.HasValue);

        var valLazy = new ResettableLazyValue<int>(() => 0);
        _ = valLazy.Value;
        Assert.That(valLazy.HasValue);
    }

    [Test]
    public void CanBeInitialized()
    {
        var refLazy = new ResettableLazy<string>(() => "", "Hello World");
        Assert.That(refLazy.HasValue);
        Assert.That(refLazy.Value, Is.EqualTo("Hello World"));

        var valLazy = new ResettableLazyValue<int>(() => 0, 42);
        Assert.That(valLazy.HasValue);
        Assert.That(valLazy.Value, Is.EqualTo(42));
    }

    [Test]
    public void DoesNotCallCreatorEarly()
    {
        bool didCallRef = false;
        var refLazy = new ResettableLazy<string>(() => { didCallRef = true; return ""; });
        Assert.That(didCallRef, Is.False);
        _ = refLazy.Value;
        Assert.That(didCallRef);

        bool didCallVal = false;
        var valLazy = new ResettableLazyValue<int>(() => { didCallVal = true; return 0; });
        Assert.That(didCallVal, Is.False);
        _ = valLazy.Value;
        Assert.That(didCallVal);
    }

    [Test]
    public void CallsCreatorOnlyOnce()
    {
        int callCountRef = 0;
        var refLazy = new ResettableLazy<string>(() => { callCountRef++; return ""; });
        _ = refLazy.Value;
        _ = refLazy.Value;
        _ = refLazy.Value;
        Assert.That(callCountRef, Is.EqualTo(1));

        int callCountVal = 0;
        var valLazy = new ResettableLazyValue<int>(() => { callCountVal++; return 0; });
        _ = valLazy.Value;
        _ = valLazy.Value;
        _ = valLazy.Value;
        Assert.That(callCountVal, Is.EqualTo(1));
    }

    [Test]
    public void TakesValueFromCreator()
    {
        var refLazy = new ResettableLazy<string>(() => "Hello World");
        Assert.That(refLazy.Value, Is.EqualTo("Hello World"));

        var valLazy = new ResettableLazyValue<int>(() => 42);
        Assert.That(valLazy.Value, Is.EqualTo(42));
    }

    [Test]
    public void CanBeReset()
    {
        var refLazy = new ResettableLazy<string>(() => "");
        _ = refLazy.Value;
        refLazy.Reset();
        Assert.That(refLazy.HasValue, Is.False);

        var valLazy = new ResettableLazyValue<int>(() => 0);
        _ = valLazy.Value;
        valLazy.Reset();
        Assert.That(valLazy.HasValue, Is.False);
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
        Assert.That(refLazy.Value, Is.EqualTo("Second"));

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
        Assert.That(valLazy.Value, Is.EqualTo(1337));
    }
}
