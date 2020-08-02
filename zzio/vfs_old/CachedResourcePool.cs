using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using zzio.utils;

namespace zzio.vfs
{
    public class CachedResourcePool : IResourcePool_OLD
    {
        private IResourcePool_OLD pool;
        
        private OrderedDictionary resourceTypes = new OrderedDictionary(256); // string to ResourceType
        private OrderedDictionary directoryContents = new OrderedDictionary(16); // string to string[]

        public CachedResourcePool(IResourcePool_OLD pool)
        {
            this.pool = pool;
        }

        public string[] GetDirectoryContent(string path)
        {
            if (directoryContents.Contains(path))
                return (string[])directoryContents[path];
            
            ResourceType_OLD type = GetResourceType(path);
            if (type == ResourceType_OLD.Directory)
            {
                string[] content = pool.GetDirectoryContent(path);
                directoryContents.Add(path, content);
                return content;
            }
            return new string[0];
        }

        public Stream GetFileContent(string path)
        {
            ResourceType_OLD type = GetResourceType(path);
            if (type == ResourceType_OLD.File)
                return pool.GetFileContent(path);
            return null;
        }

        public ResourceType_OLD GetResourceType(string path)
        {
            if (resourceTypes.Contains(path))
                return (ResourceType_OLD)resourceTypes[path];
            
            ResourceType_OLD type = pool.GetResourceType(path);
            resourceTypes.Add(path, type);
            return type;
        }
    }
}
