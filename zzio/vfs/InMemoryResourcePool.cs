using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using zzio;

namespace zzio.vfs
{
    public class InMemoryResourcePool : IResourcePool
    {
        private readonly InMemoryDirectory root;
        public IResource Root => root;

        public InMemoryResourcePool()
        {
            root = new InMemoryDirectory(this, null, "");
        }

        public IResource CreateFile(string pathText, string content) => CreateFile(new FilePath(pathText), Encoding.UTF8.GetBytes(content));
        public IResource CreateFile(FilePath path, string content) => CreateFile(path, Encoding.UTF8.GetBytes(content));
        public IResource CreateFile(string pathText, byte[]? content = null) => CreateFile(new FilePath(pathText), content);
        public IResource CreateFile(FilePath path, byte[]? content = null)
        {
            var comparison = StringComparison.InvariantCultureIgnoreCase;
            if (path.IsAbsolute || path.IsDirectory)
                throw new ArgumentException("File path cannot be an absolute or a directory");

            InMemoryDirectory curDir = root;
            foreach (var nextDirName in path.Parts.SkipLast(1))
            {
                if (curDir.Directories.FirstOrDefault(d => d.Name.Equals(nextDirName, comparison)) is not InMemoryDirectory nextDir)
                {
                    nextDir = new InMemoryDirectory(this, curDir, curDir.Path.Combine(nextDirName));
                    curDir.directories.Add(nextDir);
                }
                curDir = nextDir;
            }

            var fileName = path.Parts.Last();
            if (curDir.Any(r => r.Name.Equals(fileName, comparison)))
                throw new ArgumentException($"Resource at path {path} already exists");
            var newFile = new InMemoryFile(curDir, curDir.Path.Combine(fileName), content); // recombine for case preservation
            curDir.files.Add(newFile);
            return newFile;
        }

        private class InMemoryDirectory : IResource
        {
            public readonly List<InMemoryDirectory> directories = new List<InMemoryDirectory>();
            public readonly List<InMemoryFile> files = new List<InMemoryFile>();

            public ResourceType Type => ResourceType.Directory;
            public FilePath Path { get; }
            public IResourcePool Pool { get; }
            public IResource? Parent { get; }
            public Stream? OpenContent() => null;
            public IEnumerable<IResource> Files => files;
            public IEnumerable<IResource> Directories => directories;

            public InMemoryDirectory(IResourcePool pool, IResource? parent, string pathText) : this(pool, parent, new FilePath(pathText)) { }
            public InMemoryDirectory(IResourcePool pool, IResource? parent, FilePath path)
            {
                Pool = pool;
                Path = path;
                Parent = parent;
            }
        }

        private class InMemoryFile : IResource
        {
            public readonly byte[] content;

            public ResourceType Type => ResourceType.File;
            public FilePath Path { get; }
            public IResourcePool Pool { get; }
            public IResource Parent { get; }
            public IEnumerator<IResource> GetEnumerator() => Enumerable.Empty<IResource>().GetEnumerator();
            public Stream OpenContent() => new MemoryStream(content, false);

            public InMemoryFile(IResource parent, FilePath path, byte[]? content = null)
            {
                Path = path;
                Pool = parent.Pool;
                Parent = parent;
                this.content = content ?? Array.Empty<byte>();
            }
        }
    }
}
