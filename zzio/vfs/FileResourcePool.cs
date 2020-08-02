using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using zzio.utils;
using System.Collections;

namespace zzio.vfs
{
    public class FileResourcePool : IResourcePool
    {
        private readonly FilePath basePath;
        public IResource Root => new DirectoryResource(this, null, "");

        public FileResourcePool(string pathText) : this(new FilePath(pathText)) { }
        public FileResourcePool(FilePath basePath)
        {
            this.basePath = basePath.Absolute;
        }

        private class DirectoryResource : IResource
        {
            private readonly FileResourcePool pool;
            public FilePath Path { get; }
            public ResourceType Type => ResourceType.Directory;
            public IResourcePool Pool => pool;
            public IResource? Parent { get; }
            public Stream? OpenContent() => null;

            public DirectoryResource(FileResourcePool pool, IResource? parent, string pathText) : this(pool, parent, new FilePath(pathText)) { }
            public DirectoryResource(FileResourcePool pool, IResource? parent, FilePath path)
            {
                Path = path;
                Parent = parent;
                this.pool = pool;
            }

            public IEnumerable<IResource> Files => Directory
                .GetFiles(pool.basePath.Combine(Path).ToString())
                .Select(fullPath => Path.Combine(System.IO.Path.GetFileName(fullPath)))
                .Select(relativePath => new FileResource(pool, this, relativePath));

            public IEnumerable<IResource> Directories => Directory
                .GetDirectories(pool.basePath.Combine(Path).ToString())
                .Select(fullPath => Path.Combine(System.IO.Path.GetFileName(fullPath)))
                .Select(relativePath => new DirectoryResource(pool, this, relativePath));
        }

        private class FileResource : IResource
        {
            private readonly FileResourcePool pool;
            public FilePath Path { get; }
            public ResourceType Type => ResourceType.File;
            public IResourcePool Pool => pool;
            public IResource? Parent { get; }
            public IEnumerator<IResource> GetEnumerator() => Enumerable.Empty<IResource>().GetEnumerator();

            public FileResource(FileResourcePool pool, IResource? parent, FilePath path)
            {
                Path = path;
                Parent = parent;
                this.pool = pool;
            }

            public Stream? OpenContent()
            {
                try
                {
                    var fullPath = pool.basePath.Combine(Path).ToString();
                    return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                }
                catch(IOException)
                {
                    return null;
                }
            }
        }
    }
}
