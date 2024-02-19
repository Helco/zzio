using System;
using System.IO;
using System.Linq;

namespace zzio.vfs;

public static class IResourceExtensions
{
    public static IResource? FindFile(this IResourcePool pool, string pathText) => FindFile(pool.Root, new FilePath(pathText));
    public static IResource? FindFile(this IResourcePool pool, FilePath path) => FindFile(pool.Root, path);
    public static IResource? FindFile(this IResource res, string pathText) => FindFile(res, new FilePath(pathText));
    public static IResource? FindFile(this IResource res, FilePath path)
    {
        if (path.IsAbsolute || !path.StaysInbound)
            throw new ArgumentException("Invalid file path", nameof(path));
        if (res.Type != ResourceType.Directory)
            throw new ArgumentException("Root resource has to be a directory", nameof(res));

        var comparison = StringComparison.InvariantCultureIgnoreCase;
        var curDir = res;
        foreach (var nextDirName in path.Parts.SkipLast(1))
        {
            var nextDir = curDir.Directories.FirstOrDefault(r => r.Name.Equals(nextDirName, comparison));
            if (nextDir == null)
                return null;
            curDir = nextDir;
        }

        var fileName = path.Parts.Last();
        return curDir.Files.FirstOrDefault(r => r.Name.Equals(fileName, comparison));
    }

    public static Stream? FindAndOpen(this IResourcePool pool, string pathText) => FindAndOpen(pool.Root, new FilePath(pathText));
    public static Stream? FindAndOpen(this IResourcePool pool, FilePath path) => FindAndOpen(pool.Root, path);
    public static Stream? FindAndOpen(this IResource res, string pathText) => FindAndOpen(res, new FilePath(pathText));
    public static Stream? FindAndOpen(this IResource res, FilePath path) => FindFile(res, path)?.OpenContent();

    public static byte[]? FindAndRead(this IResourcePool pool, string pathText) => FindAndRead(pool.Root, new FilePath(pathText));
    public static byte[]? FindAndRead(this IResourcePool pool, FilePath path) => FindAndRead(pool.Root, path);
    public static byte[]? FindAndRead(this IResource res, string pathText) => FindAndRead(res, new FilePath(pathText));
    public static byte[]? FindAndRead(this IResource res, FilePath path)
    {
        using var stream = FindAndOpen(res, path);
        if (stream == null)
            return null;
        var data = new byte[stream.Length];
        stream.ReadExactly(data.AsSpan());
        return data;
    }
}
