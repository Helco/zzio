using System.Collections.Generic;
using System.IO;
using System.Linq;
using zzio;

namespace zzio.vfs;

public class PAKParallelResourcePool : IResourcePool
{
    private readonly string filename;
    private readonly Stream keepAliveStream;
    private readonly long baseOffset;
    public IResource Root { get; }

    private FileStream OpenStream() => new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

    public PAKParallelResourcePool(string filename)
    {
        this.filename = filename;
        keepAliveStream = OpenStream();
        using var reader = new BinaryReader(keepAliveStream, BinaryIOExtension.Encoding, true);
        if (reader.ReadUInt32() != 0)
            throw new InvalidDataException("Invalid PAKArchive magic");

        var root = new PAKDirectory(this, null, new FilePath(""));
        Root = root;
        uint fileCount = reader.ReadUInt32();
        var directories = new Dictionary<FilePath, PAKDirectory>
        {
            { root.Path, root }
        };

        for (uint i = 0; i < fileCount; i++)
        {
            var entry = PAKArchiveEntry.ReadNew(reader);
            var dir = GetOrCreateDirectoryFor(entry.path.Parent);
            var file = new PAKFile(this, dir, entry);
            dir.FileList.Add(file);
        }

        PAKDirectory GetOrCreateDirectoryFor(FilePath? path)
        {
            if (path == null || !path.StaysInbound || path == "")
                return root!;
            else if (directories!.TryGetValue(path, out var prevDir))
                return prevDir;

            var dirsToCreate = new Stack<string>();
            dirsToCreate.Push(path.Parts.Last());
            var curParentPath = path.Parent;
            PAKDirectory? curParent = null;
            while (curParentPath != null && curParentPath.StaysInbound)
            {
                if (directories.TryGetValue(curParentPath, out curParent))
                    break;
                dirsToCreate.Push(curParentPath.Parts.Last());
                curParentPath = curParentPath.Parent;
            }
            if (curParent == null)
                curParent = root!;

            while (dirsToCreate.Any())
            {
                var newPath = curParent.Path.Combine(dirsToCreate.Pop());
                var prevParent = curParent;
                curParent = new PAKDirectory(this, prevParent, newPath);
                prevParent.DirectoryList.Add(curParent);
                directories.Add(newPath, curParent);
            }
            return curParent;
        }

        baseOffset = keepAliveStream.Position;
    }

    private sealed class PAKDirectory : IResource
    {
        public ResourceType Type => ResourceType.Directory;
        public FilePath Path { get; }
        public IResourcePool Pool { get; }
        public IResource? Parent { get; }
        public Stream? OpenContent() => null;
        public IEnumerable<IResource> Files => FileList;
        public IEnumerable<IResource> Directories => DirectoryList;
        public List<PAKFile> FileList { get; } = new List<PAKFile>();
        public List<PAKDirectory> DirectoryList { get; } = new List<PAKDirectory>();

        public PAKDirectory(IResourcePool pool, PAKDirectory? parent, FilePath path)
        {
            Pool = pool;
            Parent = parent;
            Path = path;
        }
    }

    private sealed class PAKFile : IResource
    {
        private readonly PAKParallelResourcePool pool;
        private readonly PAKArchiveEntry entry;
        public ResourceType Type => ResourceType.File;
        public FilePath Path { get; }
        public IResourcePool Pool => pool;
        public IResource? Parent { get; }
        public IEnumerator<IResource> GetEnumerator() => Enumerable.Empty<IResource>().GetEnumerator();

        public PAKFile(PAKParallelResourcePool pool, PAKDirectory parent, PAKArchiveEntry entry)
        {
            this.pool = pool;
            Parent = parent;
            Path = entry.path;
            this.entry = entry;
        }

        public Stream? OpenContent()
        {
            var stream = pool.OpenStream();
            stream.Position = pool.baseOffset + entry.offset;
            return new RangeStream(stream, entry.length, canWrite: false, shouldClose: true);
        }
    }
}
