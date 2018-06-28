using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using zzio.vfs;
using zzio.utils;

namespace zzio.tests.vfs
{
    public class DummyResourcePool : IResourcePool
    {
        private readonly byte[] fileContent;
        private readonly HashSet<string> files, directories;

        public DummyResourcePool(IEnumerable<string> files, byte[] fileContent)
        {
            this.files = new HashSet<string>(files);

            this.directories = new HashSet<string>();
            this.directories.Add("");
            foreach (string file in files)
            {
                FilePath path = new FilePath(file).Parent;
                while (path != "./" && path != null)
                {
                    directories.Add(path.ToPOSIXString());
                    path = path.Parent;
                }
            }
            this.fileContent = fileContent.ToArray();
        }

        public ResourceType GetResourceType(string path)
        {
            if (files.Contains(path))
                return ResourceType.File;
            if (directories.Contains(path) ||
                directories.Contains(path + "/"))
                return ResourceType.Directory;
            return ResourceType.NonExistant;
        }

        public Stream GetFileContent(string path)
        {
            if (files.Contains(path))
                return new System.IO.MemoryStream(fileContent, false);
            return null;
        }

        public string[] GetDirectoryContent(string path)
        {
            Func<char, bool> isSlash = ch => ch == '/';
            if (!path.EndsWith('/') && path != "")
                path += '/';
            return files
                .Concat(directories.Select(dir => dir.TrimEnd('/')))
                .Where(file =>
                    file != path &&
                    file.IndexOf(path) == 0 &&
                    file.Count(isSlash) == path.Count(isSlash)
                ).Select(file => file.Substring(path.Length))
                .ToArray();
        }
    }
}
