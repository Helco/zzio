using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zzio.utils;

namespace zzio.vfs
{
    public static class IResourceExtensions
    {
        public static Stream? FindAndOpen(this IResourcePool pool, string pathText) => FindAndOpen(pool.Root, new FilePath(pathText));
        public static Stream? FindAndOpen(this IResourcePool pool, FilePath path) => FindAndOpen(pool.Root, path);
        public static Stream? FindAndOpen(this IResource res, string pathText) => FindAndOpen(res, new FilePath(pathText));
        public static Stream? FindAndOpen(this IResource res, FilePath path)
        {
            if (path.IsAbsolute || !path.StaysInbound)
                throw new ArgumentException("Invalid file path", nameof(path));
            if (res.Type != ResourceType.Directory)
                throw new ArgumentException("Root resource has to be a directory", nameof(res));

            var comparison = StringComparison.InvariantCultureIgnoreCase;
            var curDir = res;
            foreach (var nextDirName in path.Parts.SkipLast(1))
            {
                var nextDir = curDir.Directories.FirstOrDefault(r => r.Name.Equals(nextDirName, comparison));
                if (nextDir == null)
                    return null;
                curDir = nextDir;
            }

            var fileName = path.Parts.Last();
            var file = curDir.Files.FirstOrDefault(r => r.Name.Equals(fileName, comparison));
            return file?.OpenContent();
        }
    }
}
