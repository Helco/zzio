using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using zzio.utils;

namespace zzio.vfs
{
    public class FastVirtualFileSystem : VirtualFileSystem
    {
        private Dictionary<FilePath, IResourcePool_OLD> files = new Dictionary<FilePath, IResourcePool_OLD>();

        public override void AddResourcePool(IResourcePool_OLD pool)
        {
            base.AddResourcePool(pool);

            var dirQueue = new Queue<FilePath>();
            dirQueue.Enqueue(new FilePath(""));
            while (dirQueue.Any())
            {
                var dir = dirQueue.Dequeue();
                var contents = pool.GetDirectoryContent(dir.ToPOSIXString());
                foreach (var content in contents)
                {
                    var fullPath = dir.Combine(content);
                    var type = pool.GetResourceType(fullPath.ToPOSIXString());
                    if (type == ResourceType_OLD.Directory)
                        dirQueue.Enqueue(fullPath);
                    else if (type == ResourceType_OLD.File)
                        files.TryAdd(fullPath, pool);
                    else
                        throw new InvalidProgramException("Invalid IResourcePool implementation");
                }
            }
        }

        public override ResourceType_OLD GetResourceType(string searchPath)
        {
            if (files.ContainsKey(new FilePath(searchPath).Normalized))
                return ResourceType_OLD.File;

            // either it is a directory or does not actually exist
            return base.GetResourceType(searchPath);
        }

        public override Stream GetFileContent(string stringPath)
        {
            if (!files.TryGetValue(new FilePath(stringPath).Normalized, out var pool))
                return null;
            var caseSensitivePath = findResourceIn(stringPath, pool, out var _);
            return pool.GetFileContent(caseSensitivePath.ToPOSIXString());
        }

        public override IEnumerable<FilePath> SearchFilePaths(Predicate<FilePath> filter, FilePath basePath)
        {
            return files.Keys
                .Where(path => path.RelativeTo(basePath).StaysInbound)
                .Where(path => filter(path))
                .ToImmutableArray();
        }
    }
}
