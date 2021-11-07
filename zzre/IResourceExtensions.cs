using System;
using zzio.rwbs;
using zzio.vfs;

namespace zzre
{
    public static class IResourceExtensions
    {
        public static T OpenAsRWBS<T>(this IResource resource) where T : Section
        {
            using var contentStream = resource.OpenContent();
            if (contentStream == null)
                throw new ArgumentException($"Could not open resource {resource.Path.ToPOSIXString()}");
            var root = Section.ReadNew(contentStream) as T;
            if (root == null)
                throw new ArgumentException($"Unexpected {root?.sectionId.ToString() + " section" ?? "read error"}, trying to open {resource.Path.ToPOSIXString()}");
            return root;
        }
    }
}
