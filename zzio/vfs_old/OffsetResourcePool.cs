using System;
using System.Collections.Generic;
using System.IO;
using zzio.utils;

namespace zzio.vfs
{
    public class OffsetResourcePool : IResourcePool_OLD
    {
        private readonly IResourcePool_OLD parent;
        private readonly FilePath offset;

        public OffsetResourcePool(IResourcePool_OLD parent, FilePath offset)
        {
            this.parent = parent;
            this.offset = offset;
        }

        public OffsetResourcePool(IResourcePool_OLD parent, string offsetPath) : this(parent, new FilePath(offsetPath)) { }

        public string[] GetDirectoryContent(string path) => parent
            .GetDirectoryContent(offset.Combine(path).ToPOSIXString());

        public Stream GetFileContent(string path) => parent
            .GetFileContent(offset.Combine(path).ToPOSIXString());

        public ResourceType_OLD GetResourceType(string path) => parent
            .GetResourceType(offset.Combine(path).ToPOSIXString());
    }
}
