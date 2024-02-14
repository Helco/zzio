using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using DefaultEcs.Command;

namespace zzre.game.systems;

public class RecordingSequentialSystem<T> : ISystem<T>
{
    private readonly Remotery profiler;
    private readonly List<ISystem<T>> systems = new();
    private readonly List<string> systemNames = new();
    private readonly EntityCommandRecorder recorder;

    public bool IsEnabled { get; set; } = true;
    public IReadOnlyList<ISystem<T>> Systems => systems;

    public RecordingSequentialSystem(ITagContainer diContainer)
    {
        profiler = diContainer.GetTag<Remotery>();
        if (!diContainer.TryGetTag(out recorder))
            diContainer.AddTag(recorder = new(1024 * 1024)); // 1MiB should suffice, right?
    }

    public void Dispose()
    {
        foreach (var system in Systems.OfType<IDisposable>())
            system.Dispose();
        recorder.Dispose();
    }

    public void Add(params ISystem<T>[] systems)
    {
        this.systems.AddRange(systems);
        systemNames.AddRange(systems.Select(s => s.GetType().Name));
    }

    public void Update(T state)
    {
        if (!IsEnabled)
            return;
        recorder.Execute();

        for (int i = 0; i < systems.Count; i++)
        {
            using var _ = profiler.SampleCPU(systemNames[i]);
            systems[i].Update(state);
            recorder.Execute();
        }
    }
}
