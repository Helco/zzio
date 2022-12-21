using System;
using zzio.rwbs;
using zzio.vfs;

namespace zzre;

public static class IResourceExtensions
{
    public static T OpenAsRWBS<T>(this IResource resource) where T : Section
    {
        using var contentStream = resource.OpenContent();
        if (contentStream == null)
            throw new ArgumentException($"Could not open resource {resource.Path.ToPOSIXString()}");
        var rootSection = Section.ReadNew(contentStream);
        if (rootSection is not T root)
            throw new ArgumentException($"Unexpected {rootSection?.sectionId + " section" ?? "read error"}, trying to open {resource.Path.ToPOSIXString()}");
        return root;
    }
}
