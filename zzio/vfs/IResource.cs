using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zzio.vfs
{
    [Flags]
    public enum ResourceType
    {
        File = 1 << 0,
        Directory = 1 << 1
    }

    public interface IResourcePool
    {
        IResource Root { get; }
    }

    public interface IResource : IEnumerable<IResource>, IEquatable<IResource>
    {
        ResourceType Type { get; }
        FilePath Path { get; }
        IResourcePool Pool { get; }
        IResource? Parent { get; }
        sealed string Name => Path.Parts.Last();

        Stream? OpenContent();

        // recursive default definition, please implement at least one
        IEnumerable<IResource> Files => this.Where(r => r.Type == ResourceType.File);
        IEnumerable<IResource> Directories => this.Where(r => r.Type == ResourceType.Directory);
        IEnumerator<IResource> IEnumerable<IResource>.GetEnumerator() => Directories.Concat(Files).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        bool IEquatable<IResource>.Equals(IResource? other) =>
            Pool == other?.Pool && Path == other?.Path;
    }
}
