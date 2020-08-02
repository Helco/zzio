using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using zzio.utils;

namespace zzio.vfs
{
    public class VirtualFileSystem
    {
        private static readonly Dictionary<string, Func<string, IResourcePool_OLD>> resourcePoolTypes =
            new Dictionary<string, Func<string, IResourcePool_OLD>>()
            {
                { "file", (path) => new FileResourcePool_OLD(path) },
                { "pak",  (path) => new PAKResourcePool_OLD(path) }
            };

        protected List<IResourcePool_OLD> pools = new List<IResourcePool_OLD>();

        public void AddResourcePool(string type, string path)
        {
            if (!resourcePoolTypes.ContainsKey(type.ToLower()))
                throw new NotSupportedException("Resource pool type \"" + type + "\" is not supported");
            AddResourcePool(resourcePoolTypes[type](path));
        }

        public virtual void AddResourcePool(IResourcePool_OLD pool)
        {
            pools.Add(pool);
        }

        /// finds a case-insensitive resource in a (possibly) case-sensitive pool
        /// <returns>UB if `resourceType == NonExistant`</returns>
        protected static FilePath findResourceIn(string pathString, IResourcePool_OLD pool, out ResourceType_OLD resourceType)
        {
            FilePath path = new FilePath(pathString).Normalized;
            // naive try
            resourceType = pool.GetResourceType(path.ToPOSIXString());
            if (resourceType != ResourceType_OLD.NonExistant)
                return path;

            // combine parts one by one
            StringComparison ignoreCase = StringComparison.InvariantCultureIgnoreCase;
            string[] pathParts = path.Parts;
            FilePath curPath = new FilePath("");
            try
            {
                foreach (string searchPart in pathParts)
                {
                    string goodPart = pool
                        .GetDirectoryContent(curPath.ToPOSIXString())
                        .First((part) => part.Equals(searchPart, ignoreCase));
                    curPath = curPath.Combine(goodPart);
                }
                resourceType = pool.GetResourceType(curPath.ToPOSIXString());
            }
            catch (InvalidOperationException)
            {
                return null; // when no matching file/dir could be found
            }
            catch (IOException)
            {
                return null;
            }
            return curPath;
        }

        public virtual ResourceType_OLD GetResourceType(string path)
        {
            foreach (IResourcePool_OLD pool in pools)
            {
                ResourceType_OLD type;
                findResourceIn(path, pool, out type);
                if (type != ResourceType_OLD.NonExistant)
                    return type;
            }
            return ResourceType_OLD.NonExistant;
        }

        public virtual Stream GetFileContent(string path)
        {
            foreach (IResourcePool_OLD pool in pools)
            {
                ResourceType_OLD type;
                FilePath casePath = findResourceIn(path, pool, out type);
                if (type != ResourceType_OLD.File)
                    continue;
                Stream stream = pool.GetFileContent(casePath.ToPOSIXString());
                if (stream != null)
                    return stream;
            }
            return null;
        }

        public virtual string[] GetDirectoryContent(string path)
        {
            IEnumerable<string> content = new HashSet<string>();
            foreach (IResourcePool_OLD pool in pools)
            {
                ResourceType_OLD type;
                FilePath casePath = findResourceIn(path, pool, out type);
                if (type != ResourceType_OLD.Directory)
                    continue;
                content = content.Union(
                    pool.GetDirectoryContent(casePath.ToPOSIXString())
                );
            }
            return content
                .Select(filename => filename.ToLowerInvariant())
                .ToArray();
        }

        public virtual string[] SearchFiles(Predicate<string> filter, string basePathString = "") =>
            SearchFiles(filter, new FilePath(basePathString));

        public virtual string[] SearchFiles(Predicate<string> filter, FilePath basePath) =>
            SearchFilePaths(path => filter(path.ToPOSIXString().ToLowerInvariant()), basePath)
            .Select(path => path.ToPOSIXString().ToLowerInvariant())
            .ToArray();

        public virtual IEnumerable<FilePath> SearchFilePaths(Predicate<FilePath> filter, FilePath basePath)
        {
            var result = Enumerable.Empty<FilePath>();
            FilePath[] contentPaths = GetDirectoryContent(basePath.ToPOSIXString())
                .Select(fileName => basePath.Combine(fileName))
                .ToArray();
            ResourceType_OLD[] types = contentPaths
                .Select(contentPath => GetResourceType(contentPath.ToPOSIXString()))
                .ToArray();

            for (int i = 0; i < contentPaths.Length; i++)
            {
                if (types[i] == ResourceType_OLD.Directory)
                    result = result.Concat(SearchFilePaths(filter, contentPaths[i]));
                else if (filter(contentPaths[i]))
                    result = result.Append(contentPaths[i]);
            }

            return result.ToArray();
        }
    }
}
