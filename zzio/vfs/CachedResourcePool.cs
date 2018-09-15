using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using zzio.utils;

namespace zzio.vfs
{
    public class CachedResourcePool : IResourcePool
    {
        private IResourcePool pool;
        
        private OrderedDictionary resourceTypes = new OrderedDictionary(256); // string to ResourceType
        private OrderedDictionary directoryContents = new OrderedDictionary(16); // string to string[]

        public CachedResourcePool(IResourcePool pool)
        {
            this.pool = pool;
        }

        public string[] GetDirectoryContent(string path)
        {
            if (directoryContents.Contains(path))
                return (string[])directoryContents[path];
            
            ResourceType type = GetResourceType(path);
            if (type == ResourceType.Directory)
            {
                string[] content = pool.GetDirectoryContent(path);
                directoryContents.Add(path, content);
                return content;
            }
            return new string[0];
        }

        public Stream GetFileContent(string path)
        {
            ResourceType type = GetResourceType(path);
            if (type == ResourceType.File)
                return pool.GetFileContent(path);
            return null;
        }

        public ResourceType GetResourceType(string path)
        {
            if (resourceTypes.Contains(path))
                return (ResourceType)resourceTypes[path];
            
            ResourceType type = pool.GetResourceType(path);
            resourceTypes.Add(path, type);
            return type;
        }
    }
}
