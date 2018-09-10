using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using zzio.utils;

namespace zzio.vfs
{
    public class VirtualFileSystem
    {
        private static readonly Dictionary<string, Func<string, IResourcePool>> resourcePoolTypes =
            new Dictionary<string, Func<string, IResourcePool>>()
            {
                { "file", (path) => new FileResourcePool(path) },
                { "pak",  (path) => new PAKResourcePool(path) }
            };

        protected List<IResourcePool> pools = new List<IResourcePool>();

        public void AddResourcePool(string type, string path)
        {
            if (!resourcePoolTypes.ContainsKey(type.ToLower()))
                throw new NotSupportedException("Resource pool type \"" + type + "\" is not supported");
            pools.Add(resourcePoolTypes[type](path));
        }

        public void AddResourcePool(IResourcePool pool)
        {
            pools.Add(pool);
        }

        /// finds a case-insensitive resource in a (possibly) case-sensitive pool
        /// <returns>UB if `resourceType == NonExistant`</returns>
        protected static FilePath findResourceIn(string pathString, IResourcePool pool, out ResourceType resourceType)
        {
            FilePath path = new FilePath(pathString).Normalized;
            // naive try
            resourceType = pool.GetResourceType(path.ToPOSIXString());
            if (resourceType != ResourceType.NonExistant)
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

        public virtual ResourceType GetResourceType(string path)
        {
            foreach (IResourcePool pool in pools)
            {
                ResourceType type;
                findResourceIn(path, pool, out type);
                if (type != ResourceType.NonExistant)
                    return type;
            }
            return ResourceType.NonExistant;
        }

        public virtual Stream GetFileContent(string path)
        {
            foreach (IResourcePool pool in pools)
            {
                ResourceType type;
                FilePath casePath = findResourceIn(path, pool, out type);
                if (type != ResourceType.File)
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
            foreach (IResourcePool pool in pools)
            {
                ResourceType type;
                FilePath casePath = findResourceIn(path, pool, out type);
                if (type != ResourceType.Directory)
                    continue;
                content = content.Union(
                    pool.GetDirectoryContent(casePath.ToPOSIXString())
                );
            }
            return content
                .Select(filename => filename.ToLowerInvariant())
                .ToArray();
        }
    }
}
