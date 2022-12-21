using System.Threading;

namespace zzmaps;

internal class ProgressStep
{
    private long current;

    public string Name { get; }
    public long Current => current;
    public long? Total { get; set; }

    public ProgressStep(string name) => Name = name;

    public void Increment() => Interlocked.Increment(ref current);
}
