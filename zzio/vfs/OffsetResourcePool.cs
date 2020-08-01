using System;
using System.Collections.Generic;
using System.IO;
using zzio.utils;

namespace zzio.vfs
{
    public class OffsetResourcePool : IResourcePool
    {
        private readonly IResourcePool parent;
        private readonly FilePath offset;

        public OffsetResourcePool(IResourcePool parent, FilePath offset)
        {
            this.parent = parent;
            this.offset = offset;
        }

        public OffsetResourcePool(IResourcePool parent, string offsetPath) : this(parent, new FilePath(offsetPath)) { }

        public string[] GetDirectoryContent(string path) => parent
            .GetDirectoryContent(offset.Combine(path).ToPOSIXString());

        public Stream GetFileContent(string path) => parent
            .GetFileContent(offset.Combine(path).ToPOSIXString());

        public ResourceType GetResourceType(string path) => parent
            .GetResourceType(offset.Combine(path).ToPOSIXString());
    }
}
