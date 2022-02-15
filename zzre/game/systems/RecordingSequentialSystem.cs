using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using DefaultEcs.Command;

namespace zzre.game.systems
{
    public class RecordingSequentialSystem<T> : ISystem<T>
    {
        private readonly ITagContainer diContainer;
        private readonly DefaultEcs.World world;
        private readonly List<ISystem<T>> systems = new List<ISystem<T>>();
        private readonly EntityCommandRecorder recorder = new EntityCommandRecorder(1024 * 1024); // 1MiB should suffice, right?

        public bool IsEnabled { get; set; } = true;
        public IReadOnlyList<ISystem<T>> Systems => systems;

        public RecordingSequentialSystem(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            world = diContainer.GetTag<DefaultEcs.World>();
            diContainer.AddTag(recorder);
        }

        public void Dispose()
        {
            foreach (var system in Systems.OfType<IDisposable>())
                system.Dispose();
            recorder.Dispose();
        }

        public void Add(params ISystem<T>[] systems) => this.systems.AddRange(systems);

        public void Update(T state)
        {
            if (!IsEnabled)
                return;
            recorder.Execute();

            foreach (var system in Systems)
            {
                system.Update(state);
                recorder.Execute();
            }
        }
    }
}
