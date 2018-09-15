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
        public override void AddResourcePool(IResourcePool pool)
        {
            if (!(pool is CachedResourcePool))
                pool = new CachedResourcePool(pool);
            base.AddResourcePool(pool);
        }
    }
}
