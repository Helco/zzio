using System;
using System.IO;

namespace zzio.vfs
{
    public enum ResourceType_OLD
    {
        NonExistant,
        File,
        Directory
    }

    /// <summary>An interface to a mounted set of resources</summary>
    /// Paths will always be relative, normalized, case-sensitive POSIX strings.
    /// A resource pool may be case-sensitive or case-insensitive.
    public interface IResourcePool_OLD
    {
        /// <summary>Returns the type of resource</summary>
        /// <param name="path">A normalized path string</param>
        ResourceType_OLD GetResourceType(string path);

        /// <summary>Loads a readable file by path</summary>
        /// <remarks>While the returned stream is opened, no other file may be opened</remarks>
        /// <param name="path">A normalized path string</param>
        /// <returns>A stream to the file content or `null` if no file could be found</returns>
        Stream GetFileContent(string path);

        /// <summary>Loads all filenames in a directory</summary>
        /// <param name="path">A normalized path string</param>
        /// <returns>An array of all relative filenames (including directories) in the given path (may be empty)</returns>
        string[] GetDirectoryContent(string path);
    }
}
