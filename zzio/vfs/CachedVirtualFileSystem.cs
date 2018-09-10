using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using zzio.utils;

namespace zzio.vfs
{
    public class CachedVirtualFileSystem : VirtualFileSystem
    {
        // Resolve case-insensitive paths to case-sensitives
        private struct ResolvedPath
        {
            public string path; // case-sensitive
            public int resourcePoolI;
            public ResourceType type;
        }
        private OrderedDictionary resolvedPaths = new OrderedDictionary(128); // string to ResolvedPath

        // resolving paths doesn't help with directory contents, this here does
        private OrderedDictionary directoryContents = new OrderedDictionary(16); // string to string[]

        private ResolvedPath resolvePath(string unresolvedPath)
        {
            unresolvedPath = unresolvedPath.ToLowerInvariant();
            if (resolvedPaths.Contains(unresolvedPath))
                return (ResolvedPath)resolvedPaths[unresolvedPath];
            
            ResolvedPath resolvedPath = new ResolvedPath
            {
                path = "",
                resourcePoolI = -1,
                type = ResourceType.NonExistant
            };
            for (int i = 0; i < pools.Count; i++)
            {
                FilePath path = VirtualFileSystem.findResourceIn(
                    unresolvedPath,
                    pools[i],
                    out resolvedPath.type);
                if (resolvedPath.type != ResourceType.NonExistant)
                {
                    resolvedPath.path = path.ToPOSIXString();
                    resolvedPath.resourcePoolI = i;
                    break;
                }
            }

            resolvedPaths.Add(unresolvedPath, resolvedPath);
            return resolvedPath;
        }

        public override ResourceType GetResourceType(string path)
        {
            ResolvedPath resolved = resolvePath(path);
            return resolved.type;
        }

        public override Stream GetFileContent(string path)
        {
            ResolvedPath resolved = resolvePath(path);
            if (resolved.type != ResourceType.File)
                return null;
            return pools[resolved.resourcePoolI].GetFileContent(resolved.path);
        }

        public override string[] GetDirectoryContent(string path)
        {
            path = path.ToLowerInvariant();
            if (directoryContents.Contains(path))
                return (string[])directoryContents[path];
            
            string[] content = base.GetDirectoryContent(path);
            directoryContents.Add(path, content);
            return content;
        }
    }
}
